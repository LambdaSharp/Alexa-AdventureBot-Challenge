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

namespace AdventureBot {

    public abstract class AAdventureResponse { }

    public class AdventureResponseSay : AAdventureResponse {

        //--- Fields ---
        public string Text;

        //--- Constructors ---
        public AdventureResponseSay(string text) {
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }
    }

    public class AdventureResponseDelay : AAdventureResponse {

        //--- Fields ---
        public readonly TimeSpan Delay;

        //--- Constructors ---
        public AdventureResponseDelay(TimeSpan delay) {
            Delay = delay;
        }
    }

    public class AdventureResponsePlay : AAdventureResponse {

        //--- Fields ---
        public readonly string Name;

        //--- Constructors ---
        public AdventureResponsePlay(string name) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    public class AdventureResponseNotUnderstood : AAdventureResponse { }

    public class AdventureResponseBye : AAdventureResponse { }

    public class AdventureResponseFinished : AAdventureResponse { }

    public class AdventureResponseMultiple : AAdventureResponse {

        //--- Fields ---
        public readonly IEnumerable<AAdventureResponse> Responses;

        //--- Constructors ---
        public AdventureResponseMultiple(IEnumerable<AAdventureResponse> responses) {
            Responses = responses ?? throw new ArgumentNullException(nameof(responses));
        }
    }
}