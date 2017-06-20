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
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.SimpleNotificationService;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AdventureBot.Alexa {
    public class Function {

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
        private readonly AmazonS3Client _s3Client;
        private readonly AmazonSimpleNotificationServiceClient _snsClient;
        private readonly AmazonDynamoDBClient _dynamoClient;
        private readonly string _adventureFileBucket;
        private readonly string _adventureFilePath;

        //--- Constructors ---
        public Function() {

            // read function settings
            var adventureFile = System.Environment.GetEnvironmentVariable("adventure_file");
            if(string.IsNullOrEmpty(adventureFile)) {
                throw new ArgumentException("missing S3 url for adventure json file", "adventure_file");
            }
            var adventureFileUrl = new Uri(adventureFile);
            _adventureFileBucket = adventureFileUrl.Host;
            _adventureFilePath = adventureFileUrl.AbsolutePath.Trim('/');

            // initialize clients
            _s3Client = new AmazonS3Client();
            _snsClient = new AmazonSimpleNotificationServiceClient();
            _dynamoClient = new AmazonDynamoDBClient();
        }

        //--- Methods ---
        public SkillResponse FunctionHandler(SkillRequest skill, ILambdaContext context) {

            // load adventure from S3
            var game = GameLoader.Parse(ReadTextFromS3(_s3Client, _adventureFileBucket, _adventureFilePath));

            // restore player object from session
            GamePlayer player;
            if(skill.Session.New) {

                // TODO: can we restore the player from DynamoDB?
                player = new GamePlayer(Game.StartPlaceId);
            } else {
                player = SessionLoader.Deserialize(game, skill.Session);
            }

            // decode skill request
            IEnumerable<AGameResponse> responses;
            IEnumerable<AGameResponse> reprompt = null;
            switch(skill.Request) {

            // skill was activated without an intent
            case LaunchRequest launch:
                LambdaLogger.Log($"*** INFO: launch\n");
                responses = game.TryDo(player, GameCommandType.Restart);
                reprompt = game.TryDo(player, GameCommandType.Help);
                return ResponseBuilder.Ask(
                    ConvertToSpeech(responses),
                    new Reprompt {
                        OutputSpeech = ConvertToSpeech(reprompt)
                    },
                    SessionLoader.Serialize(game, player)
                );

            // skill was activated with an intent
            case IntentRequest intent:

                // check if the intent is an adventure intent
                if(Enum.TryParse(intent.Intent.Name, true, out GameCommandType command)) {
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

                // respond with serialized player state
                if(reprompt != null) {
                    return ResponseBuilder.Ask(
                        ConvertToSpeech(responses),
                        new Reprompt {
                            OutputSpeech = ConvertToSpeech(reprompt)
                        },
                        SessionLoader.Serialize(game, player)
                    );
                }
                return ResponseBuilder.Tell(ConvertToSpeech(responses));

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
                    ssml.Add(new XElement("p", new XText("Sorry, I don't know what that means.")));
                    break;
                case GameResponseBye _:
                    ssml.Add(new XElement("p", new XText("Good bye.")));
                    break;
                case GameResponseFinished _:

                    // TODO: player is done with the adventure
                    break;
                case null:
                    LambdaLogger.Log($"ERROR: null response\n");
                    ssml.Add(new XElement("p", new XText("Sorry, I don't know what that means.")));
                    break;
                default:
                    LambdaLogger.Log($"ERROR: unknown response: {response.GetType().Name}\n");
                    ssml.Add(new XElement("p", new XText("Sorry, I don't know what that means.")));
                    break;
                }
            }
            return new SsmlOutputSpeech {
                Ssml = ssml.ToString(SaveOptions.DisableFormatting)
            };
        }
    }
}
