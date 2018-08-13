/*
 * MindTouch λ#
 * Copyright (C) 2018 MindTouch, Inc.
 * www.mindtouch.com  oss@mindtouch.com
 *
 * For community documentation and downloads visit mindtouch.com;
 * please review the licensing section.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
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

    public class Function : ALambdaFunction<SkillRequest, SkillResponse> {

        //--- Constants ---
        private const string PROMPT_WELCOME = "Welcome to your new adventure!";
        private const string PROMPT_WELCOME_BACK = "Welcome back to your adventure!";
        private const string PROMPT_MISUNDERSTOOD = "Sorry, I didn't understand your response.";
        private const string PROMPT_GOODBYE = "Good bye.";
        private const string PROMPT_OOPS = "Oops, something went wrong. Please try again.";
        private const string SESSION_STATE_KEY = "adventure-state";

        //--- Fields ---
        private AmazonS3Client _s3Client;
        private AmazonSimpleNotificationServiceClient _snsClient;
        private AmazonDynamoDBClient _dynamoClient;
        private string _adventureFileBucket;
        private string _adventureFileKey;
        private string _adventureSoundFilesPublicUrl;
        private string _adventurePlayerTable;
        private string _adventurePlayerFinishedTopic;

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
            LogInfo($"ADVENTURE_FILE = s3://{_adventureFileBucket}/{_adventureFileKey}");

            // read location of sound files
            var adventureSoundFiles = new Uri(config.ReadText("SoundFiles"));
            _adventureSoundFilesPublicUrl = $"https://{adventureSoundFiles.Host}.s3.amazonaws.com{adventureSoundFiles.AbsolutePath}";
            LogInfo($"SOUND_FILES = {_adventureSoundFilesPublicUrl}");

            // read topic ARN for sending notifications
            _adventurePlayerFinishedTopic = config.ReadText("AdventureFinishedTopic");

            // read DynamoDB name for player state
            _adventurePlayerTable = config.ReadText("PlayerTable");
            return Task.CompletedTask;
        }

        public override async Task<SkillResponse> ProcessMessageAsync(SkillRequest skill, ILambdaContext context) {
            try {

                // load adventure from S3
                string source;
                try {
                    using(var s3Response = await _s3Client.GetObjectAsync(_adventureFileBucket, _adventureFileKey)) {
                        var memory = new MemoryStream();
                        await s3Response.ResponseStream.CopyToAsync(memory);
                        source = Encoding.UTF8.GetString(memory.ToArray());
                    }
                } catch(AmazonS3Exception e) when(e.StatusCode == HttpStatusCode.NotFound) {
                    throw new Exception($"unable to load file from 's3://{_adventureFileBucket}/{_adventureFileKey}'");
                }

                // process adventure file
                var adventure = Adventure.Parse(source, Path.GetExtension(_adventureFileKey));

                // restore player object from session
                var state = await RestoreAdventureState(adventure, skill.Session);
                var engine = new AdventureEngine(adventure, state);
                LogInfo($"player status: {state.Status}");

                // decode skill request
                IOutputSpeech response = null;
                IOutputSpeech reprompt = null;
                switch(skill.Request) {

                // skill was activated without an intent
                case LaunchRequest launch:
                    LogInfo("launch");

                    // kick off the adventure!
                    if(state.Status == AdventureStatus.New) {
                        state.Status = AdventureStatus.InProgress;
                        response = Do(engine, AdventureCommandType.Restart, new XElement("speak", new XElement("p", new XText(PROMPT_WELCOME))));
                    } else {
                        response = Do(engine, AdventureCommandType.Describe, new XElement("speak", new XElement("p", new XText(PROMPT_WELCOME_BACK))));
                    }
                    reprompt = Do(engine, AdventureCommandType.Help);
                    break;

                // skill was activated with an intent
                case IntentRequest intent:
                    var isAdventureCommand = Enum.TryParse(intent.Intent.Name, true, out AdventureCommandType command);

                    // check if the intent is an adventure intent
                    if(isAdventureCommand) {
                        LogInfo($"adventure intent ({intent.Intent.Name})");
                        response = Do(engine, command);
                        reprompt = Do(engine, AdventureCommandType.Help);
                    } else {

                        // built-in intents
                        switch(intent.Intent.Name) {
                        case BuiltInIntent.Help:
                            LogInfo($"built-in help intent ({intent.Intent.Name})");
                            response = Do(engine, AdventureCommandType.Help);
                            reprompt = Do(engine, AdventureCommandType.Help);
                            break;
                        case BuiltInIntent.Stop:
                        case BuiltInIntent.Cancel:
                            LogInfo($"built-in stop/cancel intent ({intent.Intent.Name})");
                            response = Do(engine, AdventureCommandType.Quit);
                            break;
                        default:

                            // unknown & unsupported intents
                            LogWarn("intent not recognized");
                            response = new PlainTextOutputSpeech {
                                Text = PROMPT_MISUNDERSTOOD
                            };
                            reprompt = Do(engine, AdventureCommandType.Help);
                            break;
                        }
                    }
                    break;

                // skill session ended (no response expected)
                case SessionEndedRequest ended:
                    LogInfo("session ended");
                    return ResponseBuilder.Empty();

                // exception reported on previous response (no response expected)
                case SystemExceptionRequest error:
                    LogWarn($"skill request exception: {JsonConvert.SerializeObject(skill)}");
                    return ResponseBuilder.Empty();

                // unknown skill received (no response expected)
                default:
                    LogWarn($"unrecognized skill request: {JsonConvert.SerializeObject(skill)}");
                    return ResponseBuilder.Empty();
                }

                // check if the player reached the end
                if(adventure.Places[state.CurrentPlaceId].Finished) {
                    state.End = DateTime.UtcNow;

                    // send out notification when player reaches the end
                    if(_adventurePlayerFinishedTopic != null) {
                        LogInfo("sending out player completion information");
                        await _snsClient.PublishAsync(_adventurePlayerFinishedTopic, JsonConvert.SerializeObject(state, Formatting.None));
                    }
                }

                // create/update player record so we can continue in a future session
                await StoreAdventureState(state);

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
            } catch(Exception e) {
                LogError(e, "exception during skill processing");
                return ResponseBuilder.Tell(new PlainTextOutputSpeech {
                    Text = PROMPT_OOPS
                });
            }
        }

        private async Task<AdventureState> RestoreAdventureState(Adventure adventure, Session session) {
            var recordId = UserIdToSessionRecordKey(session.User.UserId);
            AdventureState state = null;
            if(session.New) {

                // check if the adventure state can be restored from the player table
                if(_adventurePlayerTable != null) {

                    // check if a session can be restored from the database
                    var record = await _dynamoClient.GetItemAsync(_adventurePlayerTable, new Dictionary<string, AttributeValue> {
                        ["PlayerId"] = new AttributeValue { S = recordId }
                    });
                    if(record.IsItemSet) {
                        state = JsonConvert.DeserializeObject<AdventureState>(record.Item["State"].S);
                        LogInfo($"restored state from player table\n{record.Item["State"].S}");

                        // check if the place the player was in still exists or if the player had reached an end state
                        if(!adventure.Places.TryGetValue(state.CurrentPlaceId, out AdventurePlace place)) {
                            LogWarn($"unable to find matching place for restored state from player table (value: '{state.CurrentPlaceId}')");

                            // reset player
                            state.Reset(Adventure.StartPlaceId);
                        } else if(place.Finished) {
                            LogInfo("restored player had reached end place");

                            // reset player
                            state.Reset(Adventure.StartPlaceId);
                        } else if(state.CurrentPlaceId == Adventure.StartPlaceId) {

                            // reset player
                            state.Reset(Adventure.StartPlaceId);
                        }
                    } else {
                        LogInfo("no previous state found in player table");
                    }
                }
            } else {

                // attempt to deserialize the player information
                if(!session.Attributes.TryGetValue(SESSION_STATE_KEY, out object playerStateValue) || !(playerStateValue is JObject playerState)) {
                    LogWarn($"unable to find player state in session (type: {playerStateValue?.GetType().Name})\n" + JsonConvert.SerializeObject(session));
                } else {
                    state = playerState.ToObject<AdventureState>();

                    // validate the adventure still has a matching place for the player
                    if(!adventure.Places.ContainsKey(state.CurrentPlaceId)) {
                        LogWarn($"unable to find matching place for restored player in session (value: '{state.CurrentPlaceId}')\n" + JsonConvert.SerializeObject(session));

                        // reset player
                        state.Reset(Adventure.StartPlaceId);
                    }
                }
            }

            // create new player if no player was restored
            if(state == null) {
                LogInfo("new player session started");
                state = new AdventureState(recordId, Adventure.StartPlaceId);
            }
            return state;

            // local functions
            string UserIdToSessionRecordKey(string userId) {
                var md5 = System.Security.Cryptography.MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(userId));
                return $"resume-{new Guid(md5):N}";
            }
        }

        private async Task StoreAdventureState(AdventureState state) {
            if(_adventurePlayerTable != null) {
                var jsonState = JsonConvert.SerializeObject(state, Formatting.None);
                LogInfo($"storing state in player table\n{jsonState}");
                await _dynamoClient.PutItemAsync(_adventurePlayerTable, new Dictionary<string, AttributeValue> {
                    ["PlayerId"] = new AttributeValue { S = state.RecordId },
                    ["State"] = new AttributeValue { S = jsonState },
                    ["Expire"] = new AttributeValue { N = ToEpoch(DateTime.UtcNow.AddDays(30)).ToString() }
                });
            }

            // local functions
            uint ToEpoch(DateTime date) {
                return  (uint)date.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            }
        }

        private IOutputSpeech Do(AdventureEngine engine, AdventureCommandType command, XElement ssml = null) {
            ssml = ssml ?? new XElement("speak");
            ProcessResponse(engine.Do(command));
            return new SsmlOutputSpeech {
                Ssml = ssml.ToString(SaveOptions.DisableFormatting)
            };

            // local functions
            void ProcessResponse(AAdventureResponse response) {
                switch(response) {
                case AdventureResponseSay say:
                    ssml.Add(new XElement("p", new XText(say.Text)));
                    break;
                case AdventureResponseDelay delay:
                    ssml.Add(new XElement("break", new XAttribute("time", (int)delay.Delay.TotalMilliseconds + "ms")));
                    break;
                case AdventureResponsePlay play:
                    ssml.Add(new XElement("audio", new XAttribute("src", _adventureSoundFilesPublicUrl + play.Name)));
                    break;
                case AdventureResponseNotUnderstood _:
                    ssml.Add(new XElement("p", new XText(PROMPT_MISUNDERSTOOD)));
                    break;
                case AdventureResponseBye _:
                    ssml.Add(new XElement("p", new XText(PROMPT_GOODBYE)));
                    break;
                case AdventureResponseFinished _:
                    break;
                case AdventureResponseMultiple multiple:
                    foreach(var nestedResponse in multiple.Responses) {
                        ProcessResponse(nestedResponse);
                    }
                    break;
                default:
                    throw new AdventureException($"Unknown response type: {response?.GetType().FullName}");
                }
            }
        }
    }
}
