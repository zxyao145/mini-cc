using Mapster;
using MediatR;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
using MiniCc.Api.Core.TagNs.Application.Commands;
using MiniCc.Api.Core.TagNs.Application.DTOs;
using MiniCc.Api.Core.TagNs.Application.Queries;
using MiniCc.Api.Core.TagNs.Application.Services;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Common;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public class AddTagToArticleCommandHandler : IRequestHandler<AddTagToArticleCommand, Result<TagDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<AddTagToArticleCommandHandler> _logger;
    private readonly ITagService _tagService;

    public AddTagToArticleCommandHandler(
        IUnitOfWork unitOfWork,
        IArticleDomainService articleDomainService,
        ILogger<AddTagToArticleCommandHandler> logger,
        IMediator mediator,
        ITagService tagService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _mediator = mediator;
        _tagService = tagService;
    }

    public async Task<Result<TagDto>> Handle(AddTagToArticleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var article = await _unitOfWork.Articles.GetByIdAsync(request.ArticleId, cancellationToken);
            if (article == null)
            {
                return Result<TagDto>.Failure("article not found");
            }

            var tagResult = await _mediator.Send(new GetTagByNameQuery(request.Name));
            if (tagResult.IsFailure)
            {
                return Result<TagDto>.Failure(tagResult.Error);
            }
            var tag = tagResult.Value;
            if (tag == null)
            {
                var createTagCommand = new CreateTagCommand(request.Name, request.Color);
                tag = await _tagService.CreateTagAsync(createTagCommand);
            }


            if (!article.Tags.Any(t => t.Name == tag.Name))
            {
                article.Tags.Add(tag);
            }
            await _unitOfWork.SaveChangesAsync();
            return Result<TagDto>.Success(tag.Adapt<TagDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving highlight from URL: {ArticleId}", request.ArticleId);
            return Result<TagDto>.Failure($"Failed to save article: {ex.Message}");
        }
    }
}
