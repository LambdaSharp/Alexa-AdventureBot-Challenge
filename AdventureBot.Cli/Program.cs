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

namespace AdventureBot.Cli {

    public class Program : IGameEngineDependencyProvider {

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
                game = GameLoader.LoadFrom(args[0]);
                state = new GameState("cli", Game.StartPlaceId);
            } catch(GameLoaderException e) {
                Console.WriteLine($"ERROR: {e.Message}");
                return;
            } catch(Exception e) {
                Console.WriteLine($"ERROR: unable to load file");
                Console.WriteLine(e);
                return;
            }

            // invoke game
            var app = new Program();
            GameEngine engine = new GameEngine(game, state, app);
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
            Console.Clear();

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

        //--- IGameEngineDependencyProvider Members ---
        void IGameEngineDependencyProvider.Say(string text) {
            TypeLine(text);
        }

        void IGameEngineDependencyProvider.Delay(TimeSpan delay) {
            System.Threading.Thread.Sleep(delay);
        }

        void IGameEngineDependencyProvider.Play(string url) {
            Console.WriteLine($"({url})");
        }

        void IGameEngineDependencyProvider.NotUnderstood() {
            TypeLine("Sorry, I don't know what that means.");
        }

        void IGameEngineDependencyProvider.Bye() {
            TypeLine("Good bye.");
            System.Environment.Exit(0);
        }

        void IGameEngineDependencyProvider.Error(string description) {
            throw new Exception(description);
        }
    }
}
