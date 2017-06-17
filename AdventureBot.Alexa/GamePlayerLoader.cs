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

using System.Collections.Generic;
using Alexa.NET.Request;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AdventureBot.Alexa {
    public static class SessionLoader {

        //--- Class Methods ---
        public static Session Serialize(Game game, GamePlayer player) {
            return new Session {
                Attributes = new Dictionary<string, object> {
                    ["player"] = new Dictionary<string, object> {
                        ["place-id"] = player.Place.Id
                    }
                }
            };
        }

        public static GamePlayer Deserialize(Game game, Session session) {
            if(session.New) {
                LambdaLogger.Log("*** INFO: new player session started\n");
                return new GamePlayer(game.Places["start"]);
            }
            if(!session.Attributes.TryGetValue("player", out object playerStateValue) || !(playerStateValue is JObject playerState)) {
                LambdaLogger.Log($"*** WARNING: unable to find player state in session (type: {playerStateValue?.GetType().Name})\n");
                LambdaLogger.Log(JsonConvert.SerializeObject(session) + "\n");
                return new GamePlayer(game.Places["start"]);
            }
            if(!playerState.TryGetValue("place-id", out Newtonsoft.Json.Linq.JToken placeIdToken) || !(placeIdToken is JValue placeIdValue)) {
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
    }
}