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
using Amazon.Lambda.Core;
using Amazon.S3;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AdventureBot.Alexa {
    public class Function {

        //--- Class Methods ---
        public static string ReadTextFromS3(AmazonS3Client s3Client, string bucket, string filepath) {
            using(var response = s3Client.GetObjectAsync(bucket, filepath).Result) {
                if(response.HttpStatusCode != HttpStatusCode.OK) {
                    throw new Exception($"unable to load file from 's3://{bucket}/{filepath}'");
                }
                return Encoding.UTF8.GetString(ReadStream(response.ResponseStream));
            }

            byte[] ReadStream(Stream stream) {
                var memory = new MemoryStream();
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        public static IEnumerable<AGameResponse> TryDo(Game game, GamePlayer player, GameCommandType command) {
            try {
                return game.Do(player, command);
            } catch(GameException e) {
                LambdaLogger.Log($"*** ERROR: a game exception occurred ({e.Message})\n");
                return new[] { new GameResponseSay("") };
            } catch(Exception e) {
                LambdaLogger.Log($"*** ERROR: {e}\n");
                return new[] { new GameResponseSay("Oops, something went wrong. Please try again.") };
            }
        }

        public static IOutputSpeech ConvertToSpeech(IEnumerable<AGameResponse> responses) {
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
            return new SsmlOutputSpeech { Ssml = ssml.ToString(SaveOptions.DisableFormatting) };
        }

        public static Session SerializeGameSession(Game game, GamePlayer player) {
            return new Session {
                Attributes = new Dictionary<string, object> {
                    ["player"] = new Dictionary<string, object> {
                        ["place-id"] = player.Place.Id
                    }
                }
            };
        }

        public static GamePlayer DeserializeGameSession(Game game, Session session) {
            if(session.New) {
                LambdaLogger.Log("*** INFO: new player session started\n");
                return new GamePlayer(game.Places["start"]);
            }
            if(!session.Attributes.TryGetValue("player", out object playerStateValue) || !(playerStateValue is JObject playerState)) {
                LambdaLogger.Log($"*** WARNING: unable to find player state in session (type: {playerStateValue?.GetType().Name})\n");
                LambdaLogger.Log(JsonConvert.SerializeObject(session) + "\n");
                return new GamePlayer(game.Places["start"]);
            }
            if(!playerState.TryGetValue("place-id", out JToken placeIdToken) || !(placeIdToken is JValue placeIdValue)) {
                LambdaLogger.Log($"*** WARNING: unable to find place ID for player in session (type: {placeIdToken?.GetType().Name})\n");
                LambdaLogger.Log(JsonConvert.SerializeObject(session) + "\n");
                return new GamePlayer(game.Places["start"]);
            }
            if(!game.Places.TryGetValue((string)placeIdValue, out GamePlace place)) {
                LambdaLogger.Log($"*** WARNING: unable to find matching place for place ID in session (value: '{(string)placeIdValue}')\n");
                LambdaLogger.Log(JsonConvert.SerializeObject(session) + "\n");
                return new GamePlayer(game.Places["start"]);
            }
            return new GamePlayer(place);
        }


        //--- Fields ---
        private readonly AmazonS3Client _s3Client;
        private readonly string _adventureFileBucket;
        private readonly string _adventureFilePath;

        //--- Constructors ---
        public Function() {
            _s3Client = new AmazonS3Client();
            var adventureFileUrl = new Uri(System.Environment.GetEnvironmentVariable("adventure_file"));
            _adventureFileBucket = adventureFileUrl.Host;
            _adventureFilePath = adventureFileUrl.AbsolutePath.Trim('/');
        }

        //--- Methods ---
        public SkillResponse FunctionHandler(SkillRequest skill, ILambdaContext context) {

            // load adventure from S3
            var game = GameLoader.Parse(ReadTextFromS3(_s3Client, _adventureFileBucket, _adventureFilePath));

            // restore player object from session
            var player = DeserializeGameSession(game, skill.Session);

            // decode skill request
            IEnumerable<AGameResponse> responses;
            IEnumerable<AGameResponse> reprompt = null;
            switch(skill.Request) {

            // skill was activated without an intent
            case LaunchRequest launch:
                LambdaLogger.Log($"*** INFO: launch\n");
                responses = TryDo(game, player, GameCommandType.Restart);
                reprompt = TryDo(game, player, GameCommandType.Help);
                return ResponseBuilder.Ask(
                    ConvertToSpeech(responses),
                    new Reprompt {
                        OutputSpeech = ConvertToSpeech(reprompt)
                    },
                    SerializeGameSession(game, player)
                );

            // skill was activated with an intent
            case IntentRequest intent:

                // check if the intent is an adventure intent
                if(Enum.TryParse(intent.Intent.Name, true, out GameCommandType command)) {
                    LambdaLogger.Log($"*** INFO: adventure intent ({intent.Intent.Name})\n");
                    responses = TryDo(game, player, command);
                    reprompt = TryDo(game, player, GameCommandType.Help);
                } else {
                    switch(intent.Intent.Name) {

                    // built-in intents
                    case BuiltInIntent.Help:
                        LambdaLogger.Log($"*** INFO: built-in help intent ({intent.Intent.Name})\n");
                        responses = TryDo(game, player, GameCommandType.Help);
                        reprompt = TryDo(game, player, GameCommandType.Help);
                        break;

                    case BuiltInIntent.Stop:
                    case BuiltInIntent.Cancel:
                        LambdaLogger.Log($"*** INFO: built-in stop/cancel intent ({intent.Intent.Name})\n");
                        responses = TryDo(game, player, GameCommandType.Quit);
                        break;

                    // unsupported built-in intents
                    case BuiltInIntent.Pause:
                    case BuiltInIntent.Resume:
                    case BuiltInIntent.LoopOff:
                    case BuiltInIntent.LoopOn:
                    case BuiltInIntent.Next:
                    case BuiltInIntent.Previous:
                    case BuiltInIntent.Repeat:
                    case BuiltInIntent.ShuffleOff:
                    case BuiltInIntent.ShuffleOn:
                    case BuiltInIntent.StartOver:
                        LambdaLogger.Log($"*** WARNING: not supported ({intent.Intent.Name})\n");
                        responses = new[] { new GameResponseNotUnderstood() };
                        reprompt = TryDo(game, player, GameCommandType.Help);
                        break;

                    // unknown intent
                    default:
                        LambdaLogger.Log("*** WARNING: intent not recognized\n");
                        responses = new[] { new GameResponseNotUnderstood() };
                        reprompt = TryDo(game, player, GameCommandType.Help);
                        break;
                    }
                }
                if(reprompt != null) {
                    return ResponseBuilder.Ask(
                        ConvertToSpeech(responses),
                        new Reprompt {
                            OutputSpeech = ConvertToSpeech(reprompt)
                        },
                        SerializeGameSession(game, player)
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
    }
}
