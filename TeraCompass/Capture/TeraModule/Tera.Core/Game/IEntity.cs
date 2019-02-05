namespace TeraCompass.Tera.Core.Game
{
    public interface IEntity
    {
        EntityId Id { get; }
        RelationType Relation { get; set; }
        bool Dead { get; set; }
        Vector3f Position { get; set; }
        int Status { get; set; }
    }
}