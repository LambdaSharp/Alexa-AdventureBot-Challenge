/*
 * MIT License
 *
 * Copyright (c) 2017 Steve Bjorg
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Alexa.NET;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Alexa.NET.Response;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.SimpleNotificationService;
using MindTouch.LambdaSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AdventureBot.Alexa {
    public class Function : ALambdaFunction<SkillRequest, SkillResponse>, IGameEngineDependencyProvider {

        //--- Constants ---
        private const string PROMPT_WELCOME = "Welcome to your new adventure!";
        private const string PROMPT_RESUME = "Would you like to continue your previous adventure?";
        private const string PROMPT_MISUNDERSTOOD = "Sorry, I didn't understand your response.";
        private const string PROMPT_GOODBYE = "Good bye.";
        private const string PROMPT_OOPS = "Oops, something went wrong. Please try again.";
        private const string SESSION_STATE_KEY = "game-state";

        //--- Class Methods ---
        private static string ReadTextFromS3(AmazonS3Client s3Client, string bucket, string filepath) {
            try {
                using(var response = s3Client.GetObjectAsync(bucket, filepath).Result) {
                    if(response.HttpStatusCode != HttpStatusCode.OK) {
                        throw new Exception($"unable to load file from 's3://{bucket}/{filepath}'");
                    }
                    var memory = new MemoryStream();
                    response.ResponseStream.CopyTo(memory);
                    return Encoding.UTF8.GetString(memory.ToArray());
                }
            } catch(Exception e) {
                Log($"*** EXCEPTION: {e}");
                return null;
            }
        }

        private static void Log(string text) {
            LambdaLogger.Log(text + "\n");
        }

        private static string UserIdToSessionRecordKey(string userId) {
            var md5 = System.Security.Cryptography.MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(userId));
            return $"resume-{new Guid(md5):N}";
        }

        private static uint ToEpoch(DateTime date) {
            return  (uint)date.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        //--- Fields ---
        private AmazonS3Client _s3Client;
        private AmazonSimpleNotificationServiceClient _snsClient;
        private AmazonDynamoDBClient _dynamoClient;
        private string _adventureFileBucket;
        private string _adventureFileKey;
        private string _adventureSoundFilesPublicUrl;
        private string _adventurePlayerTable;
        private string _adventurePlayerFinishedTopic;
        private  XElement _ssml;

        //--- Methods ---
        public override Task InitializeAsync(LambdaConfig config) {

            // initialize clients
            _s3Client = new AmazonS3Client();
            _snsClient = new AmazonSimpleNotificationServiceClient();
            _dynamoClient = new AmazonDynamoDBClient();

            // read location of adventure files
            var adventureFiles = new Uri(config.ReadText("AdventureFiles"));
            _adventureFileBucket = adventureFiles.Host;
            _adventureFileKey = adventureFiles.AbsolutePath.TrimStart('/') + config.ReadText("AdventureFile");

            // read location of sound files
            var adventureSoundFiles = new Uri(config.ReadText("SoundFiles"));
            _adventureSoundFilesPublicUrl = $"https://{adventureSoundFiles.Host}.s3.amazonaws.com{adventureSoundFiles.AbsolutePath}";

            // read topic ARN for sending notifications
            _adventurePlayerFinishedTopic = config.ReadText("AdventureFinishedTopic");

            // read DynamoDB name for player state
            _adventurePlayerTable = config.ReadText("PlayerTable");
            return Task.CompletedTask;
        }

        public override async Task<SkillResponse> ProcessMessageAsync(SkillRequest skill, ILambdaContext context) {

            // validate configuration
            var source = ReadTextFromS3(_s3Client, _adventureFileBucket, _adventureFileKey);
            if(source == null) {
                return ResponseBuilder.Tell(new PlainTextOutputSpeech {
                    Text = "There was an error loading the adventure file. " +
                        "Make sure the lambda function is properly configured and the adventure file is publicly accessible."
                });
            }

            // load adventure from S3
            Game game;
            try {
                game = GameLoader.Parse(source, Path.GetExtension(_adventureFileKey));
            } catch(Exception e) {
                Log($"*** EXCEPTION: {e}");
                return ResponseBuilder.Tell(new PlainTextOutputSpeech {
                    Text = "There was an error parsing the adventure file. " +
                        "Make sure the adventure file is properly formatted."
                });
            }

            // restore player object from session
            var state = await RestoreGameState(game, skill.Session);
            Log($"*** INFO: player status: {state.Status}");

            // decode skill request
            IOutputSpeech response;
            IOutputSpeech reprompt = null;
            var engine = new GameEngine(game, state, this);
            switch(skill.Request) {

            // skill was activated without an intent
            case LaunchRequest launch:
                Log($"*** INFO: launch");

                // check status of player
                switch(state.Status) {
                case GameStateStatus.InProgress:
                default:

                    // unknown status, pretend player is in a new state and continue
                    state.Status = GameStateStatus.New;
                    goto case GameStateStatus.New;
                case GameStateStatus.New:

                    // kick off the adventure!
                    state.Status = GameStateStatus.InProgress;

                    // TODO (2017-07-21, bjorg): need to add support to append custom statements to response
                    // Say(PROMPT_WELCOME);

                    response = TryDo(engine, GameCommandType.Restart);
                    reprompt = TryDo(engine, GameCommandType.Help);
                    break;
                case GameStateStatus.Restored:

                    // ask player if the game session should be restored from the database
                    response = new PlainTextOutputSpeech {
                        Text = PROMPT_RESUME
                    };
                    reprompt = response;
                    break;
                }
                break;

            // skill was activated with an intent
            case IntentRequest intent:
                var isGameCommand = Enum.TryParse(intent.Intent.Name, true, out GameCommandType command);

                // check status of player
                switch(state.Status) {
                default:

                    // unknown status, pretend player is in a new state and continue
                    state.Status = GameStateStatus.New;
                    goto case GameStateStatus.New;
                case GameStateStatus.New:

                    // adventure is in progress, mark player status accordingly
                    state.Status = GameStateStatus.InProgress;
                    goto case GameStateStatus.InProgress;
                case GameStateStatus.InProgress:

                    // check if the intent is an adventure intent
                    if(isGameCommand) {
                        Log($"*** INFO: adventure intent ({intent.Intent.Name})");
                        response = TryDo(engine, command);
                        reprompt = TryDo(engine, GameCommandType.Help);
                    } else {
                        switch(intent.Intent.Name) {

                        // built-in intents
                        case BuiltInIntent.Help:
                            Log($"*** INFO: built-in help intent ({intent.Intent.Name})");
                            response = TryDo(engine, GameCommandType.Help);
                            reprompt = TryDo(engine, GameCommandType.Help);
                            break;

                        case BuiltInIntent.Stop:
                        case BuiltInIntent.Cancel:
                            Log($"*** INFO: built-in stop/cancel intent ({intent.Intent.Name})");
                            response = TryDo(engine, GameCommandType.Quit);
                            break;

                        // unknown & unsupported intents
                        default:
                            Log("*** WARNING: intent not recognized");
                            response = new PlainTextOutputSpeech {
                                Text = PROMPT_MISUNDERSTOOD
                            };
                            reprompt = TryDo(engine, GameCommandType.Help);
                            break;
                        }
                    }
                    break;
                case GameStateStatus.Restored:

                    // check if the intent is an adventure intent
                    if(isGameCommand) {
                        Log($"*** INFO: adventure intent ({intent.Intent.Name})");
                        switch(command) {
                        case GameCommandType.Yes:
                            state.Status = GameStateStatus.InProgress;
                            response = TryDo(engine, GameCommandType.Describe);
                            reprompt = TryDo(engine, GameCommandType.Help);
                            break;
                        case GameCommandType.No:
                            state.Status = GameStateStatus.InProgress;
                            response = TryDo(engine, GameCommandType.Restart);
                            reprompt = TryDo(engine, GameCommandType.Help);
                            break;
                        default:

                            // unexpected response; ask again
                            response = new PlainTextOutputSpeech {
                                Text = PROMPT_MISUNDERSTOOD + " " + PROMPT_RESUME
                            };
                            reprompt = new PlainTextOutputSpeech {
                                Text = PROMPT_RESUME
                            };
                            break;
                        }
                    } else {
                        switch(intent.Intent.Name) {

                        // built-in intents
                        case BuiltInIntent.Stop:
                        case BuiltInIntent.Cancel:
                            Log($"*** INFO: built-in stop/cancel intent ({intent.Intent.Name})");
                            response = TryDo(engine, GameCommandType.Quit);
                            break;

                        // unknown & unsupported intents
                        case BuiltInIntent.Help:
                        default:
                            Log("*** WARNING: intent not recognized");

                            // unexpected response; ask again
                            response = new PlainTextOutputSpeech {
                                Text = PROMPT_MISUNDERSTOOD + " " + PROMPT_RESUME
                            };
                            reprompt = new PlainTextOutputSpeech {
                                Text = PROMPT_RESUME
                            };
                            break;
                        }
                    }
                    break;
                }
                break;

            // skill session ended (no response expected)
            case SessionEndedRequest ended:
                Log("*** INFO: session ended");
                return ResponseBuilder.Empty();

            // exception reported on previous response (no response expected)
            case SystemExceptionRequest error:
                Log("*** INFO: system exception");
                Log($"*** EXCEPTION: skill request: {JsonConvert.SerializeObject(skill)}");
                return ResponseBuilder.Empty();

            // unknown skill received (no response expected)
            default:
                Log($"*** WARNING: unrecognized skill request: {JsonConvert.SerializeObject(skill)}");
                return ResponseBuilder.Empty();
            }

            // check if the player reached the end
            if(game.Places[state.CurrentPlaceId].Finished) {
                state.End = DateTime.UtcNow;

                // send out notification when player reaches the end
                if(_adventurePlayerFinishedTopic != null) {
                    Log("*** INFO: sending out player completion information");
                    await _snsClient.PublishAsync(_adventurePlayerFinishedTopic, JsonConvert.SerializeObject(state, Formatting.None));
                }
            }

            // create/update player record so we can continue in a future session
            if(_adventurePlayerTable != null) {
                Log("*** INFO: storing player in session table");
                await _dynamoClient.PutItemAsync(_adventurePlayerTable, new Dictionary<string, AttributeValue> {
                    ["Id"] = new AttributeValue { S = state.RecordId },
                    ["State"] = new AttributeValue { S = JsonConvert.SerializeObject(state, Formatting.None) },
                    ["Expire"] = new AttributeValue { N = ToEpoch(DateTime.UtcNow.AddDays(30)).ToString() }
                });
            }

            // respond with serialized player state
            if(reprompt != null) {
                return ResponseBuilder.Ask(
                    response,
                    new Reprompt {
                        OutputSpeech = reprompt
                    },
                    new Session {
                        Attributes = new Dictionary<string, object> {
                            [SESSION_STATE_KEY] = state
                        }
                    }
                );
            }
            return ResponseBuilder.Tell(response);
        }

        private async Task<GameState> RestoreGameState(Game game, Session session) {
            var recordId = UserIdToSessionRecordKey(session.User.UserId);
            GameState state = null;
            if(session.New) {

                // check if the player can be restored from the session table
                if(_adventurePlayerTable != null) {

                    // check if a session can be restored from the database
                    var record = await _dynamoClient.GetItemAsync(_adventurePlayerTable, new Dictionary<string, AttributeValue> {
                        ["Id"] = new AttributeValue { S = recordId }
                    });
                    if(record.IsItemSet) {
                        Log("*** INFO: restored player from session table");
                        state = JsonConvert.DeserializeObject<GameState>(record.Item["State"].S);
                        state.Status = GameStateStatus.Restored;

                        // check if the place the player was in still exists or if the player had reached an end state
                        if(!game.Places.TryGetValue(state.CurrentPlaceId, out GamePlace place)) {
                            Log($"*** WARNING: unable to find matching place for restored player from session table (value: '{state.CurrentPlaceId}')");
                            Log(JsonConvert.SerializeObject(session));

                            // reset player
                            state = null;
                        } else if(place.Finished) {
                            Log("*** INFO: restored player had reached end place");

                            // reset player
                            state = null;
                        }
                    }
                }
            } else {

                // attempt to deserialize the player information
                if(!session.Attributes.TryGetValue(SESSION_STATE_KEY, out object playerStateValue) || !(playerStateValue is JObject playerState)) {
                    Log($"*** WARNING: unable to find player state in session (type: {playerStateValue?.GetType().Name})\n" + JsonConvert.SerializeObject(session));
                } else {
                    state = playerState.ToObject<GameState>();

                    // validate the game still has a matching place for the player
                    if(!game.Places.ContainsKey(state.CurrentPlaceId)) {
                        Log($"*** WARNING: unable to find matching place for restored player in session (value: '{state.CurrentPlaceId}')\n" + JsonConvert.SerializeObject(session));

                        // reset player
                        state = null;
                    }
                }
            }

            // create new player if no player was restored
            if(state == null) {
                Log("*** INFO: new player session started");
                state = new GameState(recordId, Game.StartPlaceId);
            }
            return state;
        }

        private IOutputSpeech TryDo(GameEngine engine, GameCommandType command) {
            try {
                _ssml = new XElement("ssml");
                engine.Do(command);
                return new SsmlOutputSpeech {
                    Ssml = _ssml.ToString(SaveOptions.DisableFormatting)
                };
            } catch(GameException e) {
                Log($"*** ERROR: a game exception occurred ({e.Message})");
                return new PlainTextOutputSpeech {
                    Text = PROMPT_OOPS
                };
            } catch(Exception e) {
                Log($"*** ERROR: {e}");
                return new PlainTextOutputSpeech {
                    Text = PROMPT_OOPS
                };
            }
        }

        //--- IGameEngineDependencyProvider Members ---
        void IGameEngineDependencyProvider.Say(string text) {
            _ssml.Add(new XElement("p", new XText(text)));
        }

        void IGameEngineDependencyProvider.Delay(TimeSpan delay) {
            _ssml.Add(new XElement("break", new XAttribute("time", (int)delay.TotalMilliseconds + "ms")));
        }

        void IGameEngineDependencyProvider.Play(string name) {
            _ssml.Add(new XElement("audio", new XAttribute("src", _adventureSoundFilesPublicUrl + name)));
        }

        void IGameEngineDependencyProvider.NotUnderstood() {
            _ssml.Add(new XElement("p", new XText(PROMPT_MISUNDERSTOOD)));
        }

        void IGameEngineDependencyProvider.Bye() {
            _ssml.Add(new XElement("p", new XText(PROMPT_GOODBYE)));
        }

        void IGameEngineDependencyProvider.Error(string description) {
            LambdaLogger.Log($"*** ERROR: {description}\n");
        }
    }
}
