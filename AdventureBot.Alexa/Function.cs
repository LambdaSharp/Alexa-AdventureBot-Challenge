/*
 * MIT License
 *
 * Copyright (c) 2017-2018 Steve Bjorg
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
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.SimpleNotificationService;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MindTouch.LambdaSharp;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AdventureBot.Alexa {
    public class Function : ALambdaFunction<SkillRequest, SkillResponse> {

        //--- Class Methods ---
        private static string ReadTextFromS3(AmazonS3Client s3Client, string bucket, string filepath) {
            using(var response = s3Client.GetObjectAsync(bucket, filepath).Result) {
                if(response.HttpStatusCode != HttpStatusCode.OK) {
                    throw new Exception($"unable to load file from 's3://{bucket}/{filepath}'");
                }
                var memory = new MemoryStream();
                response.ResponseStream.CopyTo(memory);
                return Encoding.UTF8.GetString(memory.ToArray());
            }
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

        //--- Methods ---
        public override Task InitializeAsync(LambdaConfig config) {

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

            // initialize clients
            _s3Client = new AmazonS3Client();
            _snsClient = new AmazonSimpleNotificationServiceClient();
            _dynamoClient = new AmazonDynamoDBClient();
            return Task.CompletedTask;
        }

        public override async Task<SkillResponse> ProcessMessageAsync(SkillRequest skill, ILambdaContext context) {

            // load adventure from S3
            var game = GameLoader.Parse(ReadTextFromS3(_s3Client, _adventureFileBucket, _adventureFileKey));

            // restore player object from session
            GamePlayer player;
            if(skill.Session.New) {

                // TODO: can we restore the player from DynamoDB?
                player = new GamePlayer(Game.StartPlaceId);
            } else {
                player = DeserializeSession(game, skill.Session);
            }

            // decode skill request
            IEnumerable<AGameResponse> responses;
            IEnumerable<AGameResponse> reprompt = null;
            switch(skill.Request) {

            // skill was activated without an intent
            case LaunchRequest launch:
                LogInfo("launch");
                responses = TryDo(game, player, GameCommandType.Restart);
                reprompt = TryDo(game, player, GameCommandType.Help);
                return ResponseBuilder.Ask(
                    ConvertToSpeech(responses),
                    new Reprompt {
                        OutputSpeech = ConvertToSpeech(reprompt)
                    },
                    SerializeSession(game, player)
                );

            // skill was activated with an intent
            case IntentRequest intent:

                // check if the intent is an adventure intent
                if(Enum.TryParse(intent.Intent.Name, true, out GameCommandType command)) {
                    LogInfo($"adventure intent ({intent.Intent.Name})");
                    responses = TryDo(game, player, command);
                    reprompt = TryDo(game, player, GameCommandType.Help);
                } else {
                    switch(intent.Intent.Name) {

                    // built-in intents
                    case BuiltInIntent.Help:
                        LogInfo($"built-in help intent ({intent.Intent.Name})");
                        responses = TryDo(game, player, GameCommandType.Help);
                        reprompt = TryDo(game, player, GameCommandType.Help);
                        break;

                    case BuiltInIntent.Stop:
                    case BuiltInIntent.Cancel:
                        LogInfo($"built-in stop/cancel intent ({intent.Intent.Name})");
                        responses = TryDo(game, player, GameCommandType.Quit);
                        break;

                    // unknown & unsupported intents
                    default:
                        LogWarn("intent not recognized");
                        responses = new[] { new GameResponseNotUnderstood() };
                        reprompt = TryDo(game, player, GameCommandType.Help);
                        break;
                    }
                }

                // respond with serialized player state
                if(reprompt != null) {
                    return ResponseBuilder.Ask(
                        ConvertToSpeech(responses),
                        new Reprompt {
                            OutputSpeech = ConvertToSpeech(reprompt)
                        },
                        SerializeSession(game, player)
                    );
                }
                return ResponseBuilder.Tell(ConvertToSpeech(responses));

            // skill session ended (no response expected)
            case SessionEndedRequest ended:
                LogInfo("session ended");
                return ResponseBuilder.Empty();

            // exception reported on previous response (no response expected)
            case SystemExceptionRequest error:
                LogWarn($"system exception for\n{JsonConvert.SerializeObject(error)}");
                return ResponseBuilder.Empty();

            // unknown skill received (no response expected)
            default:
                LogWarn($"unrecognized skill request: {JsonConvert.SerializeObject(skill)}");
                return ResponseBuilder.Empty();
            }
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
                    ssml.Add(new XElement("audio", new XAttribute("src", _adventureSoundFilesPublicUrl + play.FileName)));
                    break;
                case GameResponseNotUnderstood _:
                    ssml.Add(new XElement("p", new XText("Sorry, I don't know what that means.")));
                    break;
                case GameResponseBye _:
                    ssml.Add(new XElement("p", new XText("Good bye.")));
                    break;
                case GameResponseFinished _:

                    // TODO: player is done with the adventure
                    break;
                case null:
                    LogWarn($"null response");
                    ssml.Add(new XElement("p", new XText("Sorry, I don't know what that means.")));
                    break;
                default:
                    LogWarn($"unknown response: {response.GetType().Name}");
                    ssml.Add(new XElement("p", new XText("Sorry, I don't know what that means.")));
                    break;
                }
            }
            return new SsmlOutputSpeech {
                Ssml = ssml.ToString(SaveOptions.DisableFormatting)
            };
        }

        public IEnumerable<AGameResponse> TryDo(Game game, GamePlayer player, GameCommandType command) {
            try {
                return game.Do(player, command);
            } catch(GameException e) {
                LogError(e, $"a game exception occurred");
                return new[] { new GameResponseSay("") };
            } catch(Exception e) {
                LogError(e, $"a general exception occurred");
                return new[] { new GameResponseSay("Oops, something went wrong. Please try again.") };
            }
        }

        public Session SerializeSession(Game game, GamePlayer player) {

            // return a new session object with the serialized player information
            return new Session {
                Attributes = new Dictionary<string, object> {
                    ["player"] = player
                }
            };
        }

        public GamePlayer DeserializeSession(Game game, Session session) {

            // check if the session is new and return a new player if so
            if(session.New) {
                LogInfo("new player session started");
                return new GamePlayer(Game.StartPlaceId);
            }

            // attempt to deserialize the player information
            if(!session.Attributes.TryGetValue("player", out object playerStateValue) || !(playerStateValue is JObject playerState)) {
                LogWarn($"unable to find player state in session (type: {playerStateValue?.GetType().Name})\n{JsonConvert.SerializeObject(session)}");
                return new GamePlayer(Game.StartPlaceId);
            }
            var player = playerState.ToObject<GamePlayer>();

            // validate the game still has a matching place for the player
            if(!game.Places.ContainsKey(player.PlaceId)) {
                LogWarn($"unable to find matching place for restored player in session (value: '{player.PlaceId}')\n{JsonConvert.SerializeObject(session)}");
                return new GamePlayer(Game.StartPlaceId);
            }
            return player;
        }
    }
}
