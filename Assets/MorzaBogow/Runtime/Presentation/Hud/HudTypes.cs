using UnityEngine;

namespace MorzaBogow.Presentation
{
    /// <summary>Orders chosen on the order panel that wait for a target clicked on the map.</summary>
    public enum HudOrder { None, Move, Attack, Ram, Board, Volley, Harvest, Deliver, Outpost, Escort }

    /// <summary>Critical messages stay until clicked; important and informative ones fade after a while.</summary>
    public enum HeraldLevel { Info, Important, Critical }

    public enum HeraldTopic { General, Launch, Alarm }

    /// <summary>
    /// A herald's message. EntityId points at the ship or port concerned (-1 none); GroupKey joins messages of the
    /// same kind about the same port or convoy into one entry with a count.
    /// </summary>
    public readonly struct HeraldMessage
    {
        public readonly string Text;
        public readonly HeraldLevel Level;
        public readonly Vector3? At;
        public readonly HeraldTopic Topic;
        public readonly int EntityId;
        public readonly string GroupKey;

        public HeraldMessage(string text, HeraldLevel level, Vector3? at, HeraldTopic topic, int entityId = -1, string groupKey = null)
        {
            Text = text;
            Level = level;
            At = at;
            Topic = topic;
            EntityId = entityId;
            GroupKey = groupKey ?? topic + ":" + text;
        }
    }
}
