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
using System.IO;
using System.Linq;
using AdventureBot;

namespace AventureBot.Cli {
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
            GamePlayer player;
            try {
                game = GameLoader.LoadFrom(args[0]);
                player = new GamePlayer(game.Places["start"]);
            } catch(GameLoaderException e) {
                Console.WriteLine($"ERROR: {e.Message}");
                return;
            } catch(Exception e) {
                Console.WriteLine($"ERROR: unable to load file");
                Console.WriteLine(e);
                return;
            }
            Console.Clear();

            // start the game loop
            var responses = game.Do(player, GameCommandType.Restart);
            try {
                while(true) {
                    if(PlayResponses(responses)) {
                        return;
                    }

                    // prompt user input
                    Console.Write("> ");
                    var commandText = Console.ReadLine().Trim().ToLower();
                    if(!Enum.TryParse(commandText, true, out GameCommandType command)) {
                        responses = new[] { new GameResponseNotUnderstood() };
                        continue;
                    }

                    // process user input
                    responses = game.Do(player, command);
                }
            } catch(Exception e) {
                Console.Error.WriteLine(e);
            }
        }

        private static bool PlayResponses(IEnumerable<AGameResponse> responses) {
            var quit = false;

            // process each response
            foreach(var response in responses) {
                switch(response) {
                case GameResponseSay say:
                    TypeLine(say.Text);
                    break;
                case GameResponseDelay delay:
                    System.Threading.Thread.Sleep(delay.Delay);
                    break;
                case GameResponsePlay play:
                    Console.WriteLine($"({play.Url})");
                    break;
                case GameResponseNotUnderstood _:
                    TypeLine("Sorry, I don't know what that means.");
                    break;
                case GameResponseBye _:
                    quit = true;
                    TypeLine("Good bye.");
                    break;
                case null:
                    Console.WriteLine($"ERROR: null response");
                    break;
                default:
                    Console.WriteLine($"ERROR: unknown response: {response.GetType().Name}");
                    break;
                }
            }

            // return boolean indicating if one of the response was a good-bye message
            return quit;

            void TypeLine(string text) {

                // write each character with a random delay to give the text output a typewriter feel
                var random = new Random();
                foreach(var c in text) {
                    System.Threading.Thread.Sleep((int)(random.NextDouble() * 10));
                    Console.Write(c);
                }
                Console.WriteLine();
            }
        }
    }
}
