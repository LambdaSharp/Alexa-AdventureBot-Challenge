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
using McMaster.Extensions.CommandLineUtils;

namespace AdventureBot.Cli {

    public class Program {

        //--- Class Methods ---
        public static void Main(string[] args) {
            var app = new CommandLineApplication {
                Name = "AdventureBot.Cli",
                FullName = "AdventureBot Command Line Interface",
                Description = "Choose-Your-Adventure CLI"
            };
            app.HelpOption();
            var filenameArg = app.Argument("<FILENAME>", "path to adventure file");
            app.OnExecute(() => {
                if(filenameArg.Value == null) {
                    Console.WriteLine(app.GetHelpText());
                    return;
                }
                if(!File.Exists(filenameArg.Value)) {
                    app.ShowRootCommandFullNameAndVersion();
                    Console.WriteLine("ERROR: could not find file");
                    return;
                }

                // initialize the adventure from the adventure file
                Adventure adventure;
                try {
                    adventure = Adventure.LoadFrom(filenameArg.Value);
                } catch(AdventureException e) {
                    Console.WriteLine($"ERROR: {e.Message}");
                    return;
                } catch(Exception e) {
                    Console.WriteLine($"ERROR: unable to load file");
                    Console.WriteLine(e);
                    return;
                }

                // invoke adventure
                var state = new AdventureState("cli", Adventure.StartPlaceId);
                var engine = new AdventureEngine(adventure, state);
                AdventureLoop(engine);
            });
            app.Execute(args);
        }

        private static void AdventureLoop(AdventureEngine engine) {

            // start the adventure loop
            ProcessResponse(engine.Do(AdventureCommandType.Restart));
            try {
                while(true) {

                    // prompt user input
                    Console.Write("> ");
                    var commandText = Console.ReadLine().Trim().ToLower();
                    if(!Enum.TryParse(commandText, true, out AdventureCommandType command)) {

                        // TODO (2017-07-21, bjorg): need a way to invoke a 'command not understood' reaction
                        // responses = new[] { new AdventureResponseNotUnderstood() };
                        continue;
                    }

                    // process user input
                    ProcessResponse(engine.Do(command));
                }
            } catch(Exception e) {
                Console.Error.WriteLine(e);
            }

            // local functions
            void ProcessResponse(AAdventureResponse response) {
                switch(response) {
                case AdventureResponseSay say:
                    TypeLine(say.Text);
                    break;
                case AdventureResponseDelay delay:
                    System.Threading.Thread.Sleep(delay.Delay);
                    break;
                case AdventureResponsePlay play:
                    Console.WriteLine($"({play.Name})");
                    break;
                case AdventureResponseNotUnderstood _:
                    TypeLine("Sorry, I don't know what that means.");
                    break;
                case AdventureResponseBye _:
                    TypeLine("Good bye.");
                    System.Environment.Exit(0);
                    break;
                case AdventureResponseFinished _:
                    break;
                case AdventureResponseMultiple multiple:
                    foreach(var nestedResponse in multiple.Responses) {
                        ProcessResponse(nestedResponse);
                    }
                    break;
                default:
                    throw new AdventureException($"Unknown response type: {response?.GetType().FullName}");
                }
            }
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
    }
}
