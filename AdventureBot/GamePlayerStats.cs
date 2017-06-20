using System;

namespace AdventureBot
{
        public class GamePlayerStats {

        //--- Fields ---
        public DateTime GameStarted { get; set; }
        public DateTime GameEnded { get; set; }

        //--- Constructors ---

        //--- Methods ---
        public void StartGame() {
            this.GameStarted = DateTime.UtcNow;            
        }
        public void EndGame() {
            this.GameEnded = DateTime.UtcNow;
        }

    }
}