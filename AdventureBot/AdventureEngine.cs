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

    public class AdventureEngine {

        //--- Fields ---
        private Adventure _adventure;
        private AdventureState _state;

        //--- Constructors ---
        public AdventureEngine(Adventure adventure, AdventureState state) {
            _adventure = adventure ?? throw new ArgumentNullException(nameof(adventure));
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        //--- Methods ---
        public AAdventureResponse Do(AdventureCommandType command) {
            var responses = new List<AAdventureResponse>();

            // some commands are optional and don't require to be defined for a place
            var optional = false;
            switch(command) {
            case AdventureCommandType.Describe:
            case AdventureCommandType.Help:
            case AdventureCommandType.Hint:
            case AdventureCommandType.Restart:
            case AdventureCommandType.Quit:
                optional = true;
                break;
            }

            // check if the place has associated actions for the choice
            if(!_adventure.Places.TryGetValue(_state.CurrentPlaceId, out AdventurePlace place)) {
                throw new AdventureException($"Cannot find current place: '{_state.CurrentPlaceId}'");
            }
            if(place.Choices.TryGetValue(command, out IEnumerable<KeyValuePair<AdventureActionType, string>> choice)) {
                foreach(var action in choice) {
                    switch(action.Key) {
                    case AdventureActionType.Goto:
                        if(!_adventure.Places.TryGetValue(action.Value, out place)) {
                            throw new AdventureException($"Cannot find goto place '{action.Value}'");
                        }

                        // check if we're in a new place and need to describe it
                        if(_state.CurrentPlaceId != place.Id) {
                            _state.CurrentPlaceId = place.Id;
                            DescribePlace(place);
                        }
                        break;
                    case AdventureActionType.Say:
                        responses.Add(new AdventureResponseSay(action.Value));
                        break;
                    case AdventureActionType.Pause:
                        if(!double.TryParse(action.Value, out double delayValue)) {
                            throw new AdventureException($"Delay must be a number '{action.Value}'");
                        }
                        responses.Add(new AdventureResponseDelay(TimeSpan.FromSeconds(delayValue)));
                        break;
                    case AdventureActionType.Play:
                        responses.Add(new AdventureResponsePlay(action.Value));
                        break;
                    }
                }
            } else if(!optional) {
                responses.Add(new AdventureResponseNotUnderstood());
            }
            switch(command) {
            case AdventureCommandType.Describe:
                DescribePlace(place);
                break;
            case AdventureCommandType.Help:
                responses.Add(new AdventureResponseSay(place.Instructions));
                break;
            case AdventureCommandType.Hint:

                // hints are optional; nothing else to do by default
                break;
            case AdventureCommandType.Restart:

                // check if current place has custom instructions for handling a restart
                if((choice == null) || !choice.Any(c => c.Key == AdventureActionType.Goto)) {
                    place = _adventure.Places[Adventure.StartPlaceId];
                    _state.CurrentPlaceId = place.Id;
                }
                DescribePlace(place);
                break;
            case AdventureCommandType.Quit:
                responses.Add(new AdventureResponseBye());
                break;
            }
            return (responses.Count == 1)
                ? responses.First()
                : new AdventureResponseMultiple(responses);

            // helper functions
            void DescribePlace(AdventurePlace current) {
                if((current.Description != null) && (current.Instructions != null)) {
                    responses.Add(new AdventureResponseSay(current.Description));
                    responses.Add(new AdventureResponseSay(current.Instructions));
                } else if(current.Description != null) {
                    responses.Add(new AdventureResponseSay(current.Description));
                } else if(current.Instructions != null) {
                    responses.Add(new AdventureResponseSay(current.Instructions));
                }
           }
        }
    }
}