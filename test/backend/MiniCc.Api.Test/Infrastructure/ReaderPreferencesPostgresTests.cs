using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.UserNs.Application.Commands;
using MiniCc.Api.Core.UserNs.Application.Queries;
using MiniCc.Api.Core.UserNs.Domain.AggregatesModel;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Test.Infrastructure;

[Collection("Postgres")]
public class ReaderPreferencesPostgresTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Migration_DefaultsPartialJsonAndRoundTrip()
    {
        await using var db = fixture.CreateContext();
        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(m => m.EndsWith("M2_ReaderPreferences"));
        var user = User.Create($"prefs-{Guid.NewGuid()}", "password");
        db.Users.Add(user); await db.SaveChangesAsync();
        try
        {
            var query = new GetReaderPreferencesQueryHandler(new UnitOfWork(db));
            var initial = await query.Handle(new(user.UserName), default);
            initial.Value.FontSize.Should().Be(18); initial.Value.Theme.Should().Be("system");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Users\" SET \"ReaderPreferences\" = '{{\"fontSize\":24}}'::jsonb WHERE \"Id\" = {user.Id}");
            db.ChangeTracker.Clear();
            var partial = (await query.Handle(new(user.UserName), default)).Value;
            partial.FontSize.Should().Be(24); partial.LineHeight.Should().Be(1.8); partial.Width.Should().Be("standard");
            var command = new UpdateReaderPreferencesCommand { UserName=user.UserName, FontSize=28, LineHeight=2.2, Width="wide", FontFamily="serif", Theme="sepia" };
            (await new UpdateReaderPreferencesCommandHandler(new UnitOfWork(db)).Handle(command, default)).IsSuccess.Should().BeTrue();
            db.ChangeTracker.Clear();
            var saved = (await query.Handle(new(user.UserName), default)).Value;
            saved.FontSize.Should().Be(28); saved.Theme.Should().Be("sepia"); saved.FontFamily.Should().Be("serif");
            var type = await db.Database.SqlQueryRaw<string>("SELECT data_type AS \"Value\" FROM information_schema.columns WHERE table_name='Users' AND column_name='ReaderPreferences'").SingleAsync();
            type.Should().Be("jsonb");
        }
        finally { await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync(); }
    }

    [Theory]
    [InlineData(15,1.8,"standard","system","system")]
    [InlineData(29,1.8,"standard","system","system")]
    [InlineData(18,1.4,"standard","system","system")]
    [InlineData(18,2.3,"standard","system","system")]
    [InlineData(18,1.8,"other","system","system")]
    [InlineData(18,1.8,"standard","other","system")]
    [InlineData(18,1.8,"standard","system","other")]
    public void RejectsInvalidValues(int size,double height,string width,string font,string theme)
    {
        new UpdateReaderPreferencesCommandValidator().Validate(new UpdateReaderPreferencesCommand { FontSize=size,LineHeight=height,Width=width,FontFamily=font,Theme=theme }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RequiresCompleteObjectAndAcceptsBoundaries()
    {
        var validator = new UpdateReaderPreferencesCommandValidator();
        validator.Validate(new UpdateReaderPreferencesCommand()).IsValid.Should().BeFalse();
        validator.Validate(new UpdateReaderPreferencesCommand() { FontSize=16,LineHeight=1.5,Width="narrow",FontFamily="system",Theme="light" }).IsValid.Should().BeTrue();
        validator.Validate(new UpdateReaderPreferencesCommand() { FontSize=28,LineHeight=2.2,Width="wide",FontFamily="serif",Theme="dark" }).IsValid.Should().BeTrue();
    }
}
