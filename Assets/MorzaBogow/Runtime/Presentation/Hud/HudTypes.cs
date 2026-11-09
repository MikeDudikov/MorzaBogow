using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>Orders chosen on the order panel that wait for a target clicked on the map.</summary>
    public enum HudOrder { None, Move, Attack, Ram, Board, Volley, Harvest, Deliver, Outpost, Escort }

    /// <summary>Critical messages stay until clicked; important and informative ones fade after a while.</summary>
    public enum HeraldLevel { Info, Important, Critical }

    public enum HeraldTopic { General, Launch, Alarm }

    public readonly struct HeraldMessage
    {
        public readonly string Text;
        public readonly HeraldLevel Level;
        public readonly Vector3? At;
        public readonly HeraldTopic Topic;

        public HeraldMessage(string text, HeraldLevel level, Vector3? at, HeraldTopic topic)
        {
            Text = text;
            Level = level;
            At = at;
            Topic = topic;
        }
    }
}
