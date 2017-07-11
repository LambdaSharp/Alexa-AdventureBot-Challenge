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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AdventureBot.Alexa {
    public class Function {

        //--- Constants ---
        private const string RESUME = "Would you like to continue your previous adventure?";
        private const string MISUNDERSTOOD = "Sorry, I didn't understand your response.";
        private const string GOODBYE = "Good bye.";

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
                LambdaLogger.Log($"*** EXCEPTION: {e}\n");
                return null;
            }
        }

        private static string UserIdToSessionRecordKey(string userId) {
            var md5 = System.Security.Cryptography.MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(userId));
            return $"resume-{new Guid(md5):N}";
        }

        //--- Fields ---
        private readonly AmazonS3Client _s3Client;
        private readonly AmazonSimpleNotificationServiceClient _snsClient;
        private readonly AmazonDynamoDBClient _dynamoClient;
        private readonly string _adventureFileBucket;
        private readonly string _adventureFilePath;
        private readonly string _tableName;
        private readonly string _gameFinishedTopic;

        //--- Constructors ---
        public Function() {

            // read function settings
            var adventureFile = System.Environment.GetEnvironmentVariable("adventure_file");
            if(Uri.TryCreate(adventureFile, UriKind.Absolute, out Uri adventureFileUrl)) {
                _adventureFileBucket = adventureFileUrl.Host;
                _adventureFilePath = adventureFileUrl.AbsolutePath.Trim('/');
            }
            _tableName = System.Environment.GetEnvironmentVariable("sessions_table_name");
            _gameFinishedTopic = System.Environment.GetEnvironmentVariable("game_finished_topic");

            // initialize clients
            _s3Client = new AmazonS3Client();
            _snsClient = new AmazonSimpleNotificationServiceClient();
            _dynamoClient = new AmazonDynamoDBClient();
        }

        //--- Methods ---
        public SkillResponse FunctionHandler(SkillRequest skill, ILambdaContext context) {

            // validate configuration
            var source = ReadTextFromS3(_s3Client, _adventureFileBucket, _adventureFilePath);
            if(source == null) {
                return ResponseBuilder.Tell(new PlainTextOutputSpeech {
                    Text = "There was an error loading the adventure file. " +
                        "Make sure the lambda function is properly configured and the adventure file is publicly accessible."
                });
            }

            // load adventure from S3
            Game game;
            try {
                game = GameLoader.Parse(source);
            } catch(Exception e) {
                LambdaLogger.Log($"*** EXCEPTION: {e}\n");
                return ResponseBuilder.Tell(new PlainTextOutputSpeech {
                    Text = "There was an error parsing the adventure file. " +
                        "Make sure the adventure file is properly formatted."
                });
            }

            // restore player object from session
            var player = RestoreGamePlayer(game, skill.Session);
            LambdaLogger.Log($"*** INFO: player status: {player.Status}\n");

            // decode skill request
            IEnumerable<AGameResponse> responses;
            IEnumerable<AGameResponse> reprompt = null;
            switch(skill.Request) {

            // skill was activated without an intent
            case LaunchRequest launch:
                LambdaLogger.Log($"*** INFO: launch\n");

                // check status of player
                switch(player.Status) {
                case GamePlayerStatus.InProgress:
                default:

                    // unknown status, pretend player is in a new state and continue
                    player.Status = GamePlayerStatus.New;
                    goto case GamePlayerStatus.New;
                case GamePlayerStatus.New:
                    player.Status = GamePlayerStatus.InProgress;

                    // kick off the adventure!
                    player.Status = GamePlayerStatus.InProgress;
                    responses = game.TryDo(player, GameCommandType.Restart);
                    reprompt = game.TryDo(player, GameCommandType.Help);
                    break;
                case GamePlayerStatus.Restored:

                    // ask player if the game session should be restored from the database
                    responses = new[] { new GameResponseSay(RESUME) };
                    reprompt = responses;
                    break;
                }
                break;

            // skill was activated with an intent
            case IntentRequest intent:
                var isGameCommand = Enum.TryParse(intent.Intent.Name, true, out GameCommandType command);

                // check status of player
                switch(player.Status) {
                default:

                    // unknown status, pretend player is in a new state and continue
                    player.Status = GamePlayerStatus.New;
                    goto case GamePlayerStatus.New;
                case GamePlayerStatus.New:

                    // adventure is in progress, mark player status accordingly
                    player.Status = GamePlayerStatus.InProgress;
                    goto case GamePlayerStatus.InProgress;
                case GamePlayerStatus.InProgress:

                    // check if the intent is an adventure intent
                    if(isGameCommand) {
                        LambdaLogger.Log($"*** INFO: adventure intent ({intent.Intent.Name})\n");
                        responses = game.TryDo(player, command);
                        reprompt = game.TryDo(player, GameCommandType.Help);
                    } else {
                        switch(intent.Intent.Name) {

                        // built-in intents
                        case BuiltInIntent.Help:
                            LambdaLogger.Log($"*** INFO: built-in help intent ({intent.Intent.Name})\n");
                            responses = game.TryDo(player, GameCommandType.Help);
                            reprompt = game.TryDo(player, GameCommandType.Help);
                            break;

                        case BuiltInIntent.Stop:
                        case BuiltInIntent.Cancel:
                            LambdaLogger.Log($"*** INFO: built-in stop/cancel intent ({intent.Intent.Name})\n");
                            responses = game.TryDo(player, GameCommandType.Quit);
                            break;

                        // unknown & unsupported intents
                        default:
                            LambdaLogger.Log("*** WARNING: intent not recognized\n");
                            responses = new[] { new GameResponseNotUnderstood() };
                            reprompt = game.TryDo(player, GameCommandType.Help);
                            break;
                        }
                    }
                    break;
                case GamePlayerStatus.Restored:

                    // check if the intent is an adventure intent
                    if(isGameCommand) {
                        LambdaLogger.Log($"*** INFO: adventure intent ({intent.Intent.Name})\n");
                        switch(command) {
                        case GameCommandType.Yes:
                            player.Status = GamePlayerStatus.InProgress;
                            responses = game.TryDo(player, GameCommandType.Describe);
                            reprompt = game.TryDo(player, GameCommandType.Help);
                            break;
                        case GameCommandType.No:
                            player.Status = GamePlayerStatus.InProgress;
                            responses = game.TryDo(player, GameCommandType.Restart);
                            reprompt = game.TryDo(player, GameCommandType.Help);
                            break;
                        default:

                            // unexpected response; ask again
                            responses = new[] { new GameResponseSay(MISUNDERSTOOD + " " + RESUME) };
                            reprompt = new[] { new GameResponseSay(RESUME) };
                            break;
                        }
                    } else {
                        switch(intent.Intent.Name) {

                        // built-in intents
                        case BuiltInIntent.Stop:
                        case BuiltInIntent.Cancel:
                            LambdaLogger.Log($"*** INFO: built-in stop/cancel intent ({intent.Intent.Name})\n");
                            player.Status = GamePlayerStatus.InProgress;
                            responses = game.TryDo(player, GameCommandType.Quit);
                            break;

                        // unknown & unsupported intents
                        case BuiltInIntent.Help:
                        default:
                            LambdaLogger.Log("*** WARNING: intent not recognized\n");

                            // unexpected response; ask again
                            responses = new[] { new GameResponseSay(MISUNDERSTOOD + " " + RESUME) };
                            reprompt = new[] { new GameResponseSay(RESUME) };
                            break;
                        }
                    }
                    break;
                }
                break;

            // skill session ended (no response expected)
            case SessionEndedRequest ended:
                LambdaLogger.Log("*** INFO: session ended\n");
                return ResponseBuilder.Empty();

            // exception reported on previous response (no response expected)
            case SystemExceptionRequest error:
                LambdaLogger.Log("*** INFO: system exception\n");
                LambdaLogger.Log($"*** EXCEPTION: skill request: {JsonConvert.SerializeObject(skill)}\n");
                return ResponseBuilder.Empty();

            // unknown skill received (no response expected)
            default:
                LambdaLogger.Log($"*** WARNING: unrecognized skill request: {JsonConvert.SerializeObject(skill)}\n");
                return ResponseBuilder.Empty();
            }

            // send out notification if player reaches the end
            if((_gameFinishedTopic != null) && game.Places.TryGetValue(player.PlaceId, out GamePlace place) && place.Finished) {
                _snsClient.PublishAsync(_gameFinishedTopic, JsonConvert.SerializeObject(player, Formatting.None)).Wait();
            }

            // respond with serialized player state
            var session = StoreGamePlayer(game, player);
            if(reprompt != null) {
                return ResponseBuilder.Ask(
                    ConvertToSpeech(responses),
                    new Reprompt {
                        OutputSpeech = ConvertToSpeech(reprompt)
                    },
                    session
                );
            }
            return ResponseBuilder.Tell(ConvertToSpeech(responses));
        }

        private IOutputSpeech ConvertToSpeech(IEnumerable<AGameResponse> responses) {
            var ssml = new XElement("speak");
            foreach(var response in responses) {
                switch(response) {
                case GameResponseSay say:
                    ssml.Add(new XElement("p", new XText(say.Text)));
                    break;
                case GameResponseDelay delay:
                    ssml.Add(new XElement("break", new XAttribute("time", (int)delay.Delay.TotalMilliseconds + "ms")));
                    break;
                case GameResponsePlay play:
                    ssml.Add(new XElement("audio", new XAttribute("src", play.Url)));
                    break;
                case GameResponseNotUnderstood _:
                    ssml.Add(new XElement("p", new XText(MISUNDERSTOOD)));
                    break;
                case GameResponseBye _:
                    ssml.Add(new XElement("p", new XText(GOODBYE)));
                    break;
                case null:
                    LambdaLogger.Log($"ERROR: null response\n");
                    ssml.Add(new XElement("p", new XText(MISUNDERSTOOD)));
                    break;
                default:
                    LambdaLogger.Log($"ERROR: unknown response: {response.GetType().Name}\n");
                    ssml.Add(new XElement("p", new XText(MISUNDERSTOOD)));
                    break;
                }
            }
            return new SsmlOutputSpeech {
                Ssml = ssml.ToString(SaveOptions.DisableFormatting)
            };
        }

        private GamePlayer RestoreGamePlayer(Game game, Session session) {
            var recordId = UserIdToSessionRecordKey(session.User.UserId);
            GamePlayer player = null;
            if(session.New) {

                // check if the player can be restored from the session table
                if(_tableName != null) {

                    // check if a session can be restored from the database
                    var record = _dynamoClient.GetItemAsync(_tableName, new Dictionary<string, AttributeValue> {
                        ["Id"] = new AttributeValue { S = recordId }
                    }).Result;
                    if(record.IsItemSet) {
                        LambdaLogger.Log("*** INFO: restoring player from session table\n");
                        player = JsonConvert.DeserializeObject<GamePlayer>(record.Item["State"].S);
                        player.Status = GamePlayerStatus.Restored;
                    }
                }
            } else {

                // attempt to deserialize the player information
                if(!session.Attributes.TryGetValue("player", out object playerStateValue) || !(playerStateValue is JObject playerState)) {
                    LambdaLogger.Log($"*** WARNING: unable to find player state in session (type: {playerStateValue?.GetType().Name})\n");
                    LambdaLogger.Log(JsonConvert.SerializeObject(session) + "\n");
                } else {
                    player = playerState.ToObject<GamePlayer>();

                    // validate the game still has a matching place for the player
                    if(!game.Places.ContainsKey(player.PlaceId)) {
                        LambdaLogger.Log($"*** WARNING: unable to find matching place for restored player in session (value: '{player.PlaceId}')\n");
                        LambdaLogger.Log(JsonConvert.SerializeObject(session) + "\n");
                        player = null;
                    }
                }
            }

            // create new player if no player was restored
            if(player == null) {
                LambdaLogger.Log("*** INFO: new player session started\n");
                player = new GamePlayer(recordId, Game.StartPlaceId);
            }
            return player;
        }

        private Session StoreGamePlayer(Game game, GamePlayer player) {
            if(game.Places.TryGetValue(player.PlaceId, out GamePlace place) && !place.Finished) {
                LambdaLogger.Log("*** INFO: storing player in session table\n");
                _dynamoClient.PutItemAsync(_tableName, new Dictionary<string, AttributeValue> {
                    ["Id"] = new AttributeValue { S = player.RecordId },
                    ["State"] = new AttributeValue { S = JsonConvert.SerializeObject(player, Formatting.None) }
                }).Wait();
            } else {
                LambdaLogger.Log("*** INFO: deleting player from session table\n");
                _dynamoClient.DeleteItemAsync(_tableName, new Dictionary<string, AttributeValue> {
                    ["Id"] = new AttributeValue { S = player.RecordId }
                }).Wait();
            }
            return new Session {
                Attributes = new Dictionary<string, object> {
                    ["player"] = player
                }
            };
        }
    }
}
