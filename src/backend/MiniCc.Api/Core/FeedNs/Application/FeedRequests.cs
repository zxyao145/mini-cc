using FluentValidation;
using MediatR;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Core.FeedNs.Application;

public record PreviewFeed(string Url) : IRequest<Result<FeedPreview>>;
public record CreateFeed(string FeedUrl, string Title, Guid[] TagIds) : IRequest<Result<CreateFeedResult>>;
public record ListFeeds : IRequest<Result<List<FeedDto>>>;
public record EditFeed(Guid Id, string? Title, Guid[]? TagIds, bool? Paused) : IRequest<Result<FeedDto?>>;
public record UnsubscribeFeed(Guid Id) : IRequest<Result<bool>>;
public record TrashFeedArticles(Guid Id) : IRequest<Result<FeedTrashResult?>>;
public record RefreshFeed(Guid Id) : IRequest<Result<RefreshResult?>>;
public class CreateFeedValidator : AbstractValidator<CreateFeed>
{
    public CreateFeedValidator() { RuleFor(r => r.FeedUrl).NotEmpty(); RuleFor(r => r.Title).NotEmpty().MaximumLength(500); RuleFor(r => r.TagIds).NotNull().Must(ids => ids.Length <= 20 && ids.All(id => id != Guid.Empty)).When(r => r.TagIds != null); }
}
public class EditFeedValidator : AbstractValidator<EditFeed>
{
    public EditFeedValidator() { RuleFor(r => r.Title).NotEmpty().MaximumLength(500).When(r => r.Title != null); RuleFor(r => r.TagIds).Must(ids => ids!.Length <= 20 && ids.All(id => id != Guid.Empty)).When(r => r.TagIds != null); }
}
public class FeedHandlers(FeedService service, FeedRefresher refresher) :
    IRequestHandler<PreviewFeed, Result<FeedPreview>>, IRequestHandler<CreateFeed, Result<CreateFeedResult>>,
    IRequestHandler<ListFeeds, Result<List<FeedDto>>>, IRequestHandler<EditFeed, Result<FeedDto?>>,
    IRequestHandler<UnsubscribeFeed, Result<bool>>, IRequestHandler<TrashFeedArticles, Result<FeedTrashResult?>>,
    IRequestHandler<RefreshFeed, Result<RefreshResult?>>
{
    private static async Task<Result<T>> Run<T>(Func<Task<T>> action)
    {
        try { return Result<T>.Success(await action()); }
        catch (Exception e) when (e is Infrastructure.FeedException or ArgumentException or System.Xml.XmlException or FormatException)
        { return Result<T>.Failure(e.Message); }
    }
    public Task<Result<FeedPreview>> Handle(PreviewFeed r, CancellationToken ct) => Run(() => service.PreviewAsync(r.Url, ct));
    public Task<Result<CreateFeedResult>> Handle(CreateFeed r, CancellationToken ct) => Run(() => service.CreateAsync(r.FeedUrl, r.Title, r.TagIds, ct));
    public Task<Result<List<FeedDto>>> Handle(ListFeeds r, CancellationToken ct) => Run(() => service.ListAsync(ct));
    public Task<Result<FeedDto?>> Handle(EditFeed r, CancellationToken ct) => Run(() => service.EditAsync(r.Id, r.Title, r.TagIds, r.Paused, ct));
    public Task<Result<bool>> Handle(UnsubscribeFeed r, CancellationToken ct) => Run(() => service.UnsubscribeAsync(r.Id, ct));
    public Task<Result<FeedTrashResult?>> Handle(TrashFeedArticles r, CancellationToken ct) => Run(() => service.TrashArticlesAsync(r.Id, ct));
    public Task<Result<RefreshResult?>> Handle(RefreshFeed r, CancellationToken ct) => Run<RefreshResult?>(async () =>
    { var token = await refresher.ClaimAsync(r.Id, ct); return token.HasValue ? await refresher.RefreshAsync(r.Id, token.Value, ct) : null; });
}
