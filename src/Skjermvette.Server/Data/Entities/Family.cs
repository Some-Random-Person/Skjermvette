namespace Skjermvette.Server.Data.Entities;

public class Family
{
    public Guid Id { get; set; }
    public List<ParentUser> Parents { get; } = [];
    public List<Child> Children { get; } = [];
    public List<AppDefinition> CustomApps { get; } = [];
}
