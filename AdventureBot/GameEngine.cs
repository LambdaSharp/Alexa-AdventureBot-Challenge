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

    public interface IGameEngineDependencyProvider {

        //--- Methods ---
        void Say(string text);
        void Delay(TimeSpan delay);
        void Play(string url);
        void NotUnderstood();
        void Bye();
        void Error(string description);
    }

    public class GameEngine {

        //--- Fields ---
        private IGameEngineDependencyProvider _provider;
        private Game _game;
        private GameState _state;

        //--- Constructors ---
        public GameEngine(Game game, GameState state, IGameEngineDependencyProvider provider) {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        //--- Methods ---
        public void Do(GameCommandType command) {

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
                _provider.Error($"Cannot find current place: '{_state.CurrentPlaceId}'");
                return;
            }
            if(place.Choices.TryGetValue(command, out IEnumerable<KeyValuePair<GameActionType, string>> choice)) {
                foreach(var action in choice) {
                    switch(action.Key) {
                    case GameActionType.Goto:
                        if(!_game.Places.TryGetValue(action.Value, out place)) {
                            _provider.Error($"Cannot find goto place: '{action.Value}'");
                        } else {

                            // check if we're in a new place and need to describe it
                            if(_state.CurrentPlaceId != place.Id) {
                                _state.CurrentPlaceId = place.Id;
                                DescribePlace(place);
                            }
                        }
                        break;
                    case GameActionType.Say:
                        _provider.Say(action.Value);
                        break;
                    case GameActionType.Pause:
                        if(!double.TryParse(action.Value, out double delayValue)) {
                            _provider.Error($"Delay must be a number: '{action.Value}'");
                        } else {
                            _provider.Delay(TimeSpan.FromSeconds(delayValue));
                        }
                        break;
                    case GameActionType.Play:
                        _provider.Play(action.Value);
                        break;
                    }
                }
            } else if(!optional) {
                _provider.NotUnderstood();
            }
            switch(command) {
            case GameCommandType.Describe:
                DescribePlace(place);
                break;
            case GameCommandType.Help:
                _provider.Say(place.Instructions);
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
                _provider.Bye();
                break;
            }

            // helper functions
            void DescribePlace(GamePlace current) {
                if((current.Description != null) && (current.Instructions != null)) {
                    _provider.Say(current.Description);
                    _provider.Say(current.Instructions);
                } else if(current.Description != null) {
                    _provider.Say(current.Description);
                } else if(current.Instructions != null) {
                    _provider.Say(current.Instructions);
                }
           }
        }
    }
}