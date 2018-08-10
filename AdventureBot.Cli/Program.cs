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
using System.IO;
using System.Linq;

namespace AdventureBot.Cli {

    public class Program {

        //--- Class Methods ---
        public static void Main(string[] args) {

            // check if a filepath to an adventure file was provided and that the file exists
            if(args.Length != 1) {
                Console.WriteLine("ERROR: missing path to adventure file.");
                return;
            }
            if(!File.Exists(args[0])) {
                Console.WriteLine("ERROR: cannot find file.");
                return;
            }

            // initialize the game from the adventure file
            Game game;
            GameState state;
            try {
                game = Game.LoadFrom(args[0]);
                state = new GameState("cli", Game.StartPlaceId);
            } catch(GameException e) {
                Console.WriteLine($"ERROR: {e.Message}");
                return;
            } catch(Exception e) {
                Console.WriteLine($"ERROR: unable to load file");
                Console.WriteLine(e);
                return;
            }

            // invoke game
            var app = new Program();
            var engine = new GameEngine(game, state);
            app.GameLoop(engine);
        }

        private static void TypeLine(string text = "") {

            // write each character with a random delay to give the text output a typewriter feel
            var random = new Random();
            foreach(var c in text) {
                System.Threading.Thread.Sleep((int)(random.NextDouble() * 10));
                Console.Write(c);
            }
            Console.WriteLine();
        }

        //--- Methods ---
        private void GameLoop(GameEngine engine) {

            // start the game loop
            engine.Do(GameCommandType.Restart);
            try {
                while(true) {

                    // prompt user input
                    Console.Write("> ");
                    var commandText = Console.ReadLine().Trim().ToLower();
                    if(!Enum.TryParse(commandText, true, out GameCommandType command)) {

                        // TODO (2017-07-21, bjorg): need a way to invoke a 'command not understood' reaction
                        // responses = new[] { new GameResponseNotUnderstood() };
                        continue;
                    }

                    // process user input
                    engine.Do(command);
                }
            } catch(Exception e) {
                Console.Error.WriteLine(e);
            }
        }

        // local functions
        private void ProcessResponse(AGameResponse response) {
            switch(response) {
            case GameResponseSay say:
                TypeLine(say.Text);
                break;
            case GameResponseDelay delay:
                System.Threading.Thread.Sleep(delay.Delay);
                break;
            case GameResponsePlay play:
                Console.WriteLine($"({play.Name})");
                break;
            case GameResponseNotUnderstood _:
                TypeLine("Sorry, I don't know what that means.");
                break;
            case GameResponseBye _:
                TypeLine("Good bye.");
                System.Environment.Exit(0);
                break;
            case GameResponseFinished _:
                break;
            case GameResponseMultiple multiple:
                foreach(var nestedResponse in multiple.Responses) {
                    ProcessResponse(nestedResponse);
                }
                break;
            default:
                throw new GameException($"Unknown response type: {response?.GetType().FullName}");
            }
        }
    }
}
