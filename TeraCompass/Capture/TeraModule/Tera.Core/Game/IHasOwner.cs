namespace TeraCompass.Tera.Core.Game
{
    internal interface IHasOwner
    {
        EntityId OwnerId { get; set; }
        Entity Owner { get; set; }
    }
}