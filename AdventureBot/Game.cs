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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

namespace AdventureBot {

    public class GameException : Exception {

        //--- Constructors  ---
        public GameException(string message) : base(message) { }
    }

    public class Game {

        //--- Constants ---
        public const string StartPlaceId = "start";

        //--- Class Methods ---
        public static Game LoadFrom(string filepath) {
            var source = File.ReadAllText(filepath);
            return Parse(source, Path.GetExtension(filepath));
        }

        public static Game Parse(string source, string extension) {
            switch(extension?.ToLower()) {
            case ".json":
                return ParseJson(source);
            case ".yaml":
            case ".yml":
                return ParseYaml(source);
            default:
                throw new GameException($"unsupported file format: {extension}");
            }
        }

        public static Game ParseYaml(string source) {
            using(var reader = new StringReader(source)) {
                var yaml = new DeserializerBuilder()
                    .Build()
                    .Deserialize(reader);
                var serializer = new SerializerBuilder()
                    .JsonCompatible()
                    .Build();
                var jsonText = serializer.Serialize(yaml);
                return ParseJson(jsonText);
            }
        }

        public static Game ParseJson(string source) {
            var jsonGame = JsonConvert.DeserializeObject<JObject>(source);
            var places = new Dictionary<string, GamePlace>();
            foreach(var jsonPlace in GetObject(jsonGame, "places")?.Properties() ?? Enumerable.Empty<JProperty>()) {

                // get id and description for place
                var id = jsonPlace.Name;
                var description = GetString(jsonPlace.Value, "description");
                var instructions = GetString(jsonPlace.Value, "instructions");
                bool.TryParse(GetString(jsonPlace.Value, "finished") ?? "false", out bool finished);

                // parse player choices
                var choices = new Dictionary<GameCommandType, IEnumerable<KeyValuePair<GameActionType, string>>>();
                foreach(var jsonChoice in GetObject(jsonPlace.Value, "choices")?.Properties() ?? Enumerable.Empty<JProperty>()) {

                    // parse choice command
                    var choice = (string)jsonChoice.Name;
                    if(!Enum.TryParse(choice, true, out GameCommandType command)) {
                        throw new GameException($"Illegal value for choice ({choice}) at {jsonChoice.Path}.");
                    }
                    if(jsonChoice.Value is JArray array) {
                        var actions = array.Select(item => {
                            var property = ((JObject)item).Properties().First();
                            if(!Enum.TryParse(property.Name, true, out GameActionType action)) {
                                throw new GameException($"Illegal key for action ({property.Name}) at {property.Path}.");
                            }
                            return new KeyValuePair<GameActionType, string>(action, (string)property.Value);
                        }).ToArray();
                        choices[command] = actions;
                    } else {
                       throw new GameException($"Expected object at {jsonChoice.Value.Path} but found {jsonChoice.Value?.Type.ToString() ?? "null"} instead.");
                    }
                }
                var place = new GamePlace(id, description, instructions, finished, choices);
                places[place.Id] = place;
            }

            // ensure there is a start room
            if(!places.ContainsKey(Game.StartPlaceId)) {
                places[Game.StartPlaceId] = new GamePlace(
                    Game.StartPlaceId,
                    "No start place is defined for this adventure. Please check your adventure file and try again.",
                    "Please check your adventure file and try again.",
                    false,
                    new Dictionary<GameCommandType, IEnumerable<KeyValuePair<GameActionType, string>>>()
                );
            }
            return new Game(places);

            // helper functions
            JObject GetObject(JToken json, string key) {
                if(json is JObject objOuter) {
                    var token = objOuter[key];
                    if(token == null) {
                        return null;
                    }
                    if(token is JObject objInner) {
                        return objInner;
                    }
                    throw new GameException($"Expected object at {json.Path}.{key} but found {token?.Type.ToString() ?? "null"} instead.");
                } else {
                   throw new GameException($"Expected object at {json.Path} but found {json?.Type.ToString() ?? "null"} instead.");
                }
            }

            string GetString(JToken token, string key, bool required = true) {
                if(token is JObject obj) {
                    var value = obj[key];
                    try {
                        return (string)value;
                    } catch {
                        throw new GameException($"Expected string at {token.Path}.{key} but found {token?.Type.ToString().ToLower() ?? "null"} instead.");
                    }
                } else if(required) {
                   throw new GameException($"Expected string at {token.Path} but found {token?.Type.ToString().ToLower() ?? "null"} instead.");
                }
                return null;
            }
        }

        //--- Fields ---
        public readonly Dictionary<string, GamePlace> Places;

        //--- Constructors ---
        public Game(Dictionary<string, GamePlace> places) {
            Places = places;
        }
    }
}