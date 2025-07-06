using MiniCc.Api.Common;
using MiniCc.Api.Models;

namespace MiniCc.Api.Data;

public class DbSeeder
{
    private readonly MiniCcContext _context;

       public DbSeeder(MiniCcContext context)
        {
            _context = context;
        }

    public async Task InitAsync()
    {
        _context.Database.EnsureCreated();
        if (!_context.Users.Any())
        {
            var user = new User()
            {
                Id = UuidUtil.NewGuidV7(),
                UserName = "mini_cc",
                Password = PasswordUtil.HashPassword("mini_cc_password"),
            };
            await _context.Users.AddAsync(user);

            var ak = new AccessKey()
            {
                Id = UuidUtil.NewGuidV7(),
                UserId = user.Id,
                Name = "default",
                Key = "ak_" + KeyGen.Generate(),
                Disabled = false,
                ExpiredTime = null
            };
            await _context.AccessKeys.AddAsync(ak);

            await _context.SaveChangesAsync();
        }

    }
}
