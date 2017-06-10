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
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AdventureBot {

    public class Game {

        //--- Fields ---
        public readonly Dictionary<string, GamePlace> Places = new Dictionary<string, GamePlace>();

        public readonly GamePlayer Player;

        //--- Constructors ---
        public Game(string source) {
            var jsonGame = JsonConvert.DeserializeObject<JObject>(source);
            foreach(var jsonPlace in ((JObject)jsonGame["places"]).Properties()) {
                var id = jsonPlace.Name;
                var description = (string)jsonPlace.Value["description"];
                var choices = new Dictionary<GameCommandType, IEnumerable<KeyValuePair<GameActionType, string>>>();
                foreach(var jsonChoice in ((JObject)jsonPlace.Value["choices"]).Properties()) {
                    Enum.TryParse((string)jsonChoice.Name, true, out GameCommandType command);
                    var actions = ((JArray)jsonChoice.Value).Select(item => {
                        var property = ((JObject)item).Properties().First();
                        Enum.TryParse(property.Name, true, out GameActionType action);
                        return new KeyValuePair<GameActionType, string>(action, (string)property.Value);
                    }).ToArray();
                    choices[command] = actions;
                }
                var place = new GamePlace(id, description, choices);
                Places[place.Id] = place;
            }
            Player = new GamePlayer(Places["start"]);
        }

        //--- Methods ---
        public IEnumerable<AGameResponse> Do(GameCommandType command) {
            var result = new List<AGameResponse>();
            var optional = false;
            switch(command) {
            case GameCommandType.Restart:
            case GameCommandType.Help:
            case GameCommandType.Quit:
                optional = true;
                break;
            }
            if(Player.Place.Choices.TryGetValue(command, out IEnumerable<KeyValuePair<GameActionType, string>> choice)) {
                foreach(var action in choice) {
                    switch(action.Key) {
                    case GameActionType.Goto:
                        if(!Places.TryGetValue(action.Value, out Player.Place)) {
                            throw new Exception($"cannot find place: '{action.Value}'");
                        }
                        result.Add(new GameResponseSay(Player.Place.Description));
                        break;
                    case GameActionType.Say:
                        result.Add(new GameResponseSay(action.Value));
                        break;
                    case GameActionType.Delay:
                        result.Add(new GameResponseDelay(TimeSpan.FromSeconds(double.Parse(action.Value))));
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
            case GameCommandType.Restart:
                Player.Place = Places["start"];
                result.Add(new GameResponseSay(Player.Place.Description));
                break;
            case GameCommandType.Quit:
                result.Add(new GameResponseBye());
                break;
            case GameCommandType.Help:

                // TODO: implement the help response
                throw new NotImplementedException("help is missing");
            }
            return result;
        }
    }
}