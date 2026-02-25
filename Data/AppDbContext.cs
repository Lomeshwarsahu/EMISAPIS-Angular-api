using EMISAPIS.DTOS;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace EMISAPIS.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<UserDTO> Users { get; set; }
    }
}
