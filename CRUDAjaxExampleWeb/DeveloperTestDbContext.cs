using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using CRUDAjaxExampleWeb.Models;

public partial class DeveloperTestDbContext : DbContext
{
    public DeveloperTestDbContext()
        : base("name=DeveloperTestEntities")
    {
    }

    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        throw new UnintentionalCodeFirstException();
    }

    public DbSet<Person> People { get; set; }
}