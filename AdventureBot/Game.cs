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
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AdventureBot {

    public class GameException : Exception {

        //--- Constructors  ---
        public GameException(string message) : base(message) { }
    }

    public class Game {

        //--- Fields ---
        public readonly Dictionary<string, GamePlace> Places;

        //--- Constructors ---
        public Game(Dictionary<string, GamePlace> places) {
            Places = places;
        }

        //--- Methods ---
        public IEnumerable<AGameResponse> Do(GamePlayer player, GameCommandType command) {
            var result = new List<AGameResponse>();

            // some commands are optional and don't require to be defined for a place
            var optional = false;
            switch(command) {
            case GameCommandType.Describe:
            case GameCommandType.Help:
            case GameCommandType.Hint:
            case GameCommandType.Restart:
            case GameCommandType.Quit:
                optional = true;
                break;
            }

            // check if the place has associated actions for the choice
            if(player.Place.Choices.TryGetValue(command, out IEnumerable<KeyValuePair<GameActionType, string>> choice)) {
                foreach(var action in choice) {
                    switch(action.Key) {
                    case GameActionType.Goto:
                        if(!Places.TryGetValue(action.Value, out player.Place)) {
                            throw new GameException($"Cannot find place: '{action.Value}'");
                        }
                        Describe(player.Place);
                        break;
                    case GameActionType.Say:
                        result.Add(new GameResponseSay(action.Value));
                        break;
                    case GameActionType.Pause:
                        if(!double.TryParse(action.Value, out double delayValue)) {
                            throw new GameException($"Delay must be a number: '{action.Value}'");
                        }
                        result.Add(new GameResponseDelay(TimeSpan.FromSeconds(delayValue)));
                        break;
                    case GameActionType.Play:
                        result.Add(new GameResponsePlay(action.Value));
                        break;
                    }
                }
            } else if(!optional) {
                result.Add(new GameResponseNotUnderstood());
            }
            switch(command) {
            case GameCommandType.Describe:
                Describe(player.Place);
                break;
            case GameCommandType.Help:
                result.Add(new GameResponseSay(player.Place.Instructions));
                break;
            case GameCommandType.Hint:

                // hints are optional; nothing else to do
                break;
            case GameCommandType.Restart:
                player.Place = Places["start"];
                Describe(player.Place);
                break;
            case GameCommandType.Quit:
                result.Add(new GameResponseBye());
                break;
            }
            return result;

            void Describe(GamePlace place) {
                if((place.Description != null) && (place.Instructions != null)) {
                    result.Add(new GameResponseSay(place.Description));
                    result.Add(new GameResponseSay(place.Instructions));
                } else if(place.Description != null) {
                    result.Add(new GameResponseSay(place.Description));
                } else if(place.Instructions != null) {
                    result.Add(new GameResponseSay(place.Instructions));
                }
           }
        }
    }
}