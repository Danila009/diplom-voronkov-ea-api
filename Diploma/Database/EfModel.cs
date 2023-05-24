using Diploma.model.user;
using Microsoft.EntityFrameworkCore;

namespace Diploma.Database;

public class EfModel:DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }

    public EfModel(DbContextOptions options) : base(options)
    {
        Database.EnsureCreated();
    }
    
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<Doctor> Doctors { get; set; }
    public virtual DbSet<PostDoctor> PostDoctors { get; set; }
    public virtual DbSet<Admin> Admins { get; set; }
    public virtual DbSet<Appointment> Appointments { get; set; }
    public virtual DbSet<Recipe> Recipes { get; set; }
}