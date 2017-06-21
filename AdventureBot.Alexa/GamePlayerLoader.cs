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
using Alexa.NET.Request;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AdventureBot.Alexa {
    public static class SessionLoader {

        //--- Class Methods ---
        public static Session Serialize(Game game, GamePlayer player, Action<GamePlayer> storeInDbFunc) {
            if(storeInDbFunc != null) {
                storeInDbFunc(player);
            }
            // return a new session object with the serialized player information
            return new Session {
                Attributes = new Dictionary<string, object> {
                    ["player"] = player
                }
            };
        }
        public static GamePlayer Deserialize(Game game, Session session) {

            // check if the session is new and return a new player if so
            if(session.New) {
                LambdaLogger.Log("*** INFO: new player session started\n");
                return new GamePlayer(Game.StartPlaceId);
            }

            // attempt to deserialize the player information
            if(!session.Attributes.TryGetValue("player", out object playerStateValue) || !(playerStateValue is JObject playerState)) {
                LambdaLogger.Log($"*** WARNING: unable to find player state in session (type: {playerStateValue?.GetType().Name})\n");
                LambdaLogger.Log(JsonConvert.SerializeObject(session) + "\n");
                return new GamePlayer(Game.StartPlaceId);
            }
            var player = playerState.ToObject<GamePlayer>();

            // validate the game still has a matching place for the player
            if(!game.Places.ContainsKey(player.PlaceId)) {
                LambdaLogger.Log($"*** WARNING: unable to find matching place for restored player in session (value: '{player.PlaceId}')\n");
                LambdaLogger.Log(JsonConvert.SerializeObject(session) + "\n");
                return new GamePlayer(Game.StartPlaceId);
            }
            return player;
        }
    }
}