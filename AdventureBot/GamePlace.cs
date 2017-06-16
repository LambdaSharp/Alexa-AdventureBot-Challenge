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

namespace AdventureBot {

    public enum GameCommandType {
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

    public enum GameActionType {
        Goto = 1,
        Say,
        Pause,
        Play
    }

     public class GamePlace {

        //--- Fields ---
        public readonly string Id;
        public readonly string Description;
        public readonly string Instructions;

        public readonly Dictionary<GameCommandType, IEnumerable<KeyValuePair<GameActionType, string>>> Choices;

        //--- Constructors ---
        public GamePlace(string id, string description, string instructions, Dictionary<GameCommandType, IEnumerable<KeyValuePair<GameActionType, string>>> choices) {
            Id = id;
            Description = description;
            Instructions = instructions;
            Choices = choices;
        }
    }
}