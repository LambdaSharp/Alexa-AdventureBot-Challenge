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
using System.Linq;

namespace AdventureBot {

    public class GameEngine {

        //--- Fields ---
        private Game _game;
        private GameState _state;

        //--- Constructors ---
        public GameEngine(Game game, GameState state) {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        //--- Methods ---
        public AGameResponse Do(GameCommandType command) {
            var responses = new List<AGameResponse>();

            // record that a command was issued
            ++_state.CommandsIssued;

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
            if(!_game.Places.TryGetValue(_state.CurrentPlaceId, out GamePlace place)) {
                throw new GameException($"Cannot find current place: '{_state.CurrentPlaceId}'");
            }
            if(place.Choices.TryGetValue(command, out IEnumerable<KeyValuePair<GameActionType, string>> choice)) {
                foreach(var action in choice) {
                    switch(action.Key) {
                    case GameActionType.Goto:
                        if(!_game.Places.TryGetValue(action.Value, out place)) {
                            throw new GameException($"Cannot find goto place '{action.Value}'");
                        }

                        // check if we're in a new place and need to describe it
                        if(_state.CurrentPlaceId != place.Id) {
                            _state.CurrentPlaceId = place.Id;
                            DescribePlace(place);
                        }
                        break;
                    case GameActionType.Say:
                        responses.Add(new GameResponseSay(action.Value));
                        break;
                    case GameActionType.Pause:
                        if(!double.TryParse(action.Value, out double delayValue)) {
                            throw new GameException($"Delay must be a number '{action.Value}'");
                        }
                        responses.Add(new GameResponseDelay(TimeSpan.FromSeconds(delayValue)));
                        break;
                    case GameActionType.Play:
                        responses.Add(new GameResponsePlay(action.Value));
                        break;
                    }
                }
            } else if(!optional) {
                responses.Add(new GameResponseNotUnderstood());
            }
            switch(command) {
            case GameCommandType.Describe:
                DescribePlace(place);
                break;
            case GameCommandType.Help:
                responses.Add(new GameResponseSay(place.Instructions));
                break;
            case GameCommandType.Hint:

                // hints are optional; nothing else to do by default
                break;
            case GameCommandType.Restart:

                // update player statistics
                ++_state.GameAttempts;
                _state.Start = DateTime.UtcNow;
                _state.End = null;

                // check if current place has custom instructions for handling a restart
                if((choice == null) || !choice.Any(c => c.Key == GameActionType.Goto)) {
                    place = _game.Places[Game.StartPlaceId];
                    _state.CurrentPlaceId = place.Id;
                }
                DescribePlace(place);
                break;
            case GameCommandType.Quit:
                responses.Add(new GameResponseBye());
                break;
            }
            return (responses.Count == 1)
                ? responses.First()
                : new GameResponseMultiple(responses);

            // helper functions
            void DescribePlace(GamePlace current) {
                if((current.Description != null) && (current.Instructions != null)) {
                    responses.Add(new GameResponseSay(current.Description));
                    responses.Add(new GameResponseSay(current.Instructions));
                } else if(current.Description != null) {
                    responses.Add(new GameResponseSay(current.Description));
                } else if(current.Instructions != null) {
                    responses.Add(new GameResponseSay(current.Instructions));
                }
           }
        }
    }
}