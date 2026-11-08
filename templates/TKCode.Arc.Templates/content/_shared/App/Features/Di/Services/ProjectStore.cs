using ArcStarterApp.Models;

namespace ArcStarterApp.Services;

/// <summary>Where the Projects page gets its projects. Replace <see cref="SampleProjectStore"/> with a database or a web API.</summary>
public interface IProjectStore
{
    /// <summary>The projects to list.</summary>
    IReadOnlyList<Project> GetAll();

    /// <summary>Creates a project; <paramref name="number"/> numbers the default name.</summary>
    Project Create(int number);
}

/// <summary>The sample data of Models/Project.cs.</summary>
public sealed class SampleProjectStore : IProjectStore
{
    public IReadOnlyList<Project> GetAll() => SampleProjects.All;

    public Project Create(int number) => SampleProjects.CreateNew(number);
}
