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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

namespace AdventureBot {

    public class GameLoaderException : Exception {

        //--- Constructors ---
        public GameLoaderException(string message) : base(message) { }
    }

    public static class GameLoader {

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
                throw new GameLoaderException($"unsupported file format: {extension}");
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
                        throw new GameLoaderException($"Illegal value for choice ({choice}) at {jsonChoice.Path}.");
                    }
                    if(jsonChoice.Value is JArray array) {
                        var actions = array.Select(item => {
                            var property = ((JObject)item).Properties().First();
                            if(!Enum.TryParse(property.Name, true, out GameActionType action)) {
                                throw new GameLoaderException($"Illegal key for action ({property.Name}) at {property.Path}.");
                            }
                            return new KeyValuePair<GameActionType, string>(action, (string)property.Value);
                        }).ToArray();
                        choices[command] = actions;
                    } else {
                       throw new GameLoaderException($"Expected object at {jsonChoice.Value.Path} but found {jsonChoice.Value?.Type.ToString() ?? "null"} instead.");
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
                    throw new GameLoaderException($"Expected object at {json.Path}.{key} but found {token?.Type.ToString() ?? "null"} instead.");
                } else {
                   throw new GameLoaderException($"Expected object at {json.Path} but found {json?.Type.ToString() ?? "null"} instead.");
                }
            }

            string GetString(JToken token, string key, bool required = true) {
                if(token is JObject obj) {
                    var value = obj[key];
                    try {
                        return (string)value;
                    } catch {
                        throw new GameLoaderException($"Expected string at {token.Path}.{key} but found {token?.Type.ToString().ToLower() ?? "null"} instead.");
                    }
                } else if(required) {
                   throw new GameLoaderException($"Expected string at {token.Path} but found {token?.Type.ToString().ToLower() ?? "null"} instead.");
                }
                return null;
            }
        }
    }
}