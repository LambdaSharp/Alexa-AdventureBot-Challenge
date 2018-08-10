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

    public class AdventureException : Exception {

        //--- Constructors  ---
        public AdventureException(string message) : base(message) { }
    }

    public class Adventure {

        //--- Constants ---
        public const string StartPlaceId = "start";

        //--- Class Methods ---
        public static Adventure LoadFrom(string filepath) {
            var source = File.ReadAllText(filepath);
            return Parse(source, Path.GetExtension(filepath));
        }

        public static Adventure Parse(string source, string extension) {
            switch(extension?.ToLower()) {
            case ".json":
                return ParseJson(source);
            case ".yaml":
            case ".yml":
                return ParseYaml(source);
            default:
                throw new AdventureException($"unsupported file format: {extension}");
            }
        }

        public static Adventure ParseYaml(string source) {
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

        public static Adventure ParseJson(string source) {
            var jsonAdventure = JsonConvert.DeserializeObject<JObject>(source);
            var places = new Dictionary<string, AdventurePlace>();
            foreach(var jsonPlace in GetObject(jsonAdventure, "places")?.Properties() ?? Enumerable.Empty<JProperty>()) {

                // get id and description for place
                var id = jsonPlace.Name;
                var description = GetString(jsonPlace.Value, "description");
                var instructions = GetString(jsonPlace.Value, "instructions");
                bool.TryParse(GetString(jsonPlace.Value, "finished") ?? "false", out bool finished);

                // parse player choices
                var choices = new Dictionary<AdventureCommandType, IEnumerable<KeyValuePair<AdventureActionType, string>>>();
                foreach(var jsonChoice in GetObject(jsonPlace.Value, "choices")?.Properties() ?? Enumerable.Empty<JProperty>()) {

                    // parse choice command
                    var choice = (string)jsonChoice.Name;
                    if(!Enum.TryParse(choice, true, out AdventureCommandType command)) {
                        throw new AdventureException($"Illegal value for choice ({choice}) at {jsonChoice.Path}.");
                    }
                    if(jsonChoice.Value is JArray array) {
                        var actions = array.Select(item => {
                            var property = ((JObject)item).Properties().First();
                            if(!Enum.TryParse(property.Name, true, out AdventureActionType action)) {
                                throw new AdventureException($"Illegal key for action ({property.Name}) at {property.Path}.");
                            }
                            return new KeyValuePair<AdventureActionType, string>(action, (string)property.Value);
                        }).ToArray();
                        choices[command] = actions;
                    } else {
                       throw new AdventureException($"Expected object at {jsonChoice.Value.Path} but found {jsonChoice.Value?.Type.ToString() ?? "null"} instead.");
                    }
                }
                var place = new AdventurePlace(id, description, instructions, finished, choices);
                places[place.Id] = place;
            }

            // ensure there is a start room
            if(!places.ContainsKey(Adventure.StartPlaceId)) {
                places[Adventure.StartPlaceId] = new AdventurePlace(
                    Adventure.StartPlaceId,
                    "No start place is defined for this adventure. Please check your adventure file and try again.",
                    "Please check your adventure file and try again.",
                    false,
                    new Dictionary<AdventureCommandType, IEnumerable<KeyValuePair<AdventureActionType, string>>>()
                );
            }
            return new Adventure(places);

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
                    throw new AdventureException($"Expected object at {json.Path}.{key} but found {token?.Type.ToString() ?? "null"} instead.");
                } else {
                   throw new AdventureException($"Expected object at {json.Path} but found {json?.Type.ToString() ?? "null"} instead.");
                }
            }

            string GetString(JToken token, string key, bool required = true) {
                if(token is JObject obj) {
                    var value = obj[key];
                    try {
                        return (string)value;
                    } catch {
                        throw new AdventureException($"Expected string at {token.Path}.{key} but found {token?.Type.ToString().ToLower() ?? "null"} instead.");
                    }
                } else if(required) {
                   throw new AdventureException($"Expected string at {token.Path} but found {token?.Type.ToString().ToLower() ?? "null"} instead.");
                }
                return null;
            }
        }

        //--- Fields ---
        public readonly Dictionary<string, AdventurePlace> Places;

        //--- Constructors ---
        public Adventure(Dictionary<string, AdventurePlace> places) {
            Places = places;
        }
    }
}