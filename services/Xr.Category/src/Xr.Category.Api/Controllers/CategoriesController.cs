using Microsoft.AspNetCore.Mvc;
using Xr.Category.Application;

namespace Xr.Category.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(
    ICategoryService categoryService,
    ICategoryDapperQueryService dapperQueryService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CategoryDto>> Create(
        CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await categoryService.CreateAsync(
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(FindById),
            new { id = category.Id },
            category);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> FindById(
        long id,
        CancellationToken cancellationToken)
    {
        var category = await categoryService.FindByIdAsync(
            id,
            cancellationToken);

        return category is null ? NotFound() : Ok(category);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var categories = await categoryService.ListAsync(
            page,
            pageSize,
            cancellationToken);

        return Ok(categories);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        long id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await categoryService.UpdateAsync(
            id,
            request,
            cancellationToken);

        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var deleted = await categoryService.DeleteAsync(
            id,
            cancellationToken);

        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("search/dapper")]
    public async Task<ActionResult<CategoryDapperSearchResult>> SearchWithDapper(
        [FromQuery] CategoryDapperSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await dapperQueryService.SearchAsync(
            request,
            cancellationToken);

        return Ok(result);
    }
}
