using Microsoft.AspNetCore.Mvc;
using DynamicGrid.Api.Services;   //upload the files and extract the data from them

namespace DynamicGrid.Api.Controllers;

[ApiController]
[Route("api/grid")]  //All end point url starts with here
public class GridController : ControllerBase
{
    private static readonly List<TableData> _tables = new();
    private readonly FileParserService _parser;

    private class TableData
    {
        public int Id { get; set; }
        public string TableName { get; set; } = "";
        public int RowCount { get; set; }
        public List<Dictionary<string, string>> Rows { get; set; } = new();
    }

    public GridController(FileParserService parser)
    {
        _parser = parser;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("File required");

        try
        {
            var rows = await _parser.ParseAsync(file);

            var table = new TableData
            {
                Id = _tables.Count,
                TableName = file.FileName,
                RowCount = rows.Count,
                Rows = rows
            };

            _tables.Add(table);

            return Ok(new
            {
                id = table.Id,
                tableName = table.TableName,
                rowCount = table.RowCount
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("tables")]
    public IActionResult GetTables()
    {
        
        var tableMetadata = _tables.Select(t => new
        {
            id = t.Id,
            tableName = t.TableName,
            rowCount = t.RowCount
        });
        
        return Ok(tableMetadata);
    }

    [HttpGet("tables/{id}")]
    public IActionResult GetTableData(int id)
    {
        if (id < 0 || id >= _tables.Count)
            return NotFound("Table not found");

        var table = _tables[id];
        return Ok(new
        {
            id = table.Id,
            tableName = table.TableName,
            rowCount = table.RowCount,
            rows = table.Rows
        });
    }

    [HttpDelete("tables/{id}")]
    public IActionResult DeleteTable(int id)
    {
        if (id < 0 || id >= _tables.Count)
            return NotFound("Table not found");

        _tables.RemoveAt(id);
        
        for (int i = 0; i < _tables.Count; i++)
        {
            _tables[i].Id = i;
        }

        return Ok(new { message = "Table deleted successfully" });
    }

    [HttpPut("tables/{id}/rows/{rowIndex}")]
    public IActionResult UpdateRow(int id, int rowIndex, [FromBody] Dictionary<string, string> updatedRow)
    {
        if (id < 0 || id >= _tables.Count)
            return NotFound("Table not found");

        var table = _tables[id];
        if (rowIndex < 0 || rowIndex >= table.Rows.Count)
            return NotFound("Row not found");

        table.Rows[rowIndex] = updatedRow;
        return Ok(new { message = "Row updated successfully", row = updatedRow });
    }

    [HttpDelete("tables/{id}/rows/{rowIndex}")]
    public IActionResult DeleteRow(int id, int rowIndex)
    {
        if (id < 0 || id >= _tables.Count)
            return NotFound("Table not found");

        var table = _tables[id];
        if (rowIndex < 0 || rowIndex >= table.Rows.Count)
            return NotFound("Row not found");

        table.Rows.RemoveAt(rowIndex);
        table.RowCount = table.Rows.Count;

        return Ok(new { message = "Row deleted successfully" });
    }
}
