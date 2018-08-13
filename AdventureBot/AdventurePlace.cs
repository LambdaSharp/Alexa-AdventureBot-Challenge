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

using System.Collections.Generic;

namespace AdventureBot {

    public enum AdventureCommandType {
        One = 1,
        Two,
        Three,
        Four,
        Five,
        Six,
        Seven,
        Eight,
        Nine,
        OptionOne = 1,
        OptionTwo,
        OptionThree,
        OptionFour,
        OptionFive,
        OptionSix,
        OptionSeven,
        OptionEight,
        OptionNine,
        Yes = 100,
        No,
        Describe = 200,
        Help,
        Hint,
        Restart,
        Quit
    }

    public enum AdventureActionType {
        Goto = 1,
        Say,
        Pause,
        Play
    }

     public class AdventurePlace {

        //--- Fields ---
        public readonly string Id;
        public readonly string Description;
        public readonly string Instructions;
        public readonly bool Finished;

        public readonly Dictionary<AdventureCommandType, IEnumerable<KeyValuePair<AdventureActionType, string>>> Choices;

        //--- Constructors ---
        public AdventurePlace(string id, string description, string instructions, bool finished, Dictionary<AdventureCommandType, IEnumerable<KeyValuePair<AdventureActionType, string>>> choices) {
            Id = id;
            Description = description;
            Instructions = instructions;
            Finished = finished;
            Choices = choices;
        }
    }
}