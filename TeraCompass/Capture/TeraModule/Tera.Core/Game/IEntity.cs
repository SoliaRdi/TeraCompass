namespace TeraCompass.Tera.Core.Game
{
    public interface IEntity
    {
        Angle EndAngle { get; set; }
        long EndTime { get; set; }
        Vector3f Finish { get; set; }
        Angle Heading { get; set; }
        EntityId Id { get; }
        RelationType Relation { get; set; }
        bool Dead { get; set; }
        Angle LastCastAngle { get; set; }
        Vector3f Position { get; set; }
        Entity RootOwner { get; }
        int Speed { get; set; }
        long StartTime { get; set; }
        int Status { get; set; }
        bool Equals(Entity other);
        bool Equals(object obj);
        int GetHashCode();
        string ToString();
    }
}