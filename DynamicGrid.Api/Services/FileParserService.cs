using OfficeOpenXml;
using UglyToad.PdfPig;

namespace DynamicGrid.Api.Services;

public class FileParserService
{
    public Task<List<Dictionary<string, string>>> ParseAsync(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLower();

        var result = ext switch
        {
            ".xlsx" => ParseExcel(file),
            ".csv" => ParseCsv(file),
            ".pdf" => ParsePdf(file),
            _ => throw new Exception("Unsupported file type")
        };
        
        return Task.FromResult(result);
    }

  private List<Dictionary<string, string>> ParseExcel(IFormFile file)
{
    try
    {
        var rows = new List<Dictionary<string, string>>();
        
        using var memoryStream = new MemoryStream();
        file.CopyTo(memoryStream);
        memoryStream.Position = 0;

        using var package = new ExcelPackage(memoryStream);

        if (package.Workbook.Worksheets.Count == 0)
            throw new Exception("Excel file has no worksheets");

        var sheet = package.Workbook.Worksheets[0];

        if (sheet.Dimension == null)
            return rows;

        int rowCount = sheet.Dimension.End.Row;
        int colCount = sheet.Dimension.End.Column;

       
        int headerRowIndex = -1;
        for (int r = 1; r <= rowCount; r++)
        {
            var cellTexts = new List<string>();
            int meaningfulCells = 0;
            int genericColumnCells = 0;

            for (int c = 1; c <= colCount; c++)
            {
                var cellValue = sheet.Cells[r, c].Value?.ToString() ?? "";
                cellTexts.Add(cellValue);
                
              
                if (!string.IsNullOrWhiteSpace(cellValue) && 
                    cellValue.Any(ch => char.IsLetterOrDigit(ch)))
                {
                    meaningfulCells++;
                }

                if (System.Text.RegularExpressions.Regex.IsMatch(cellValue, @"^Column\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    genericColumnCells++;
                }
            }

            if (meaningfulCells >= colCount * 0.5 && genericColumnCells < colCount * 0.5)
            {
                headerRowIndex = r;
                break;
            }
        }

        if (headerRowIndex == -1)
            return rows;

        var validColumnIndices = new List<int>();
        var headers = new List<string>();
        
        for (int c = 1; c <= colCount; c++)
        {
            var headerText = sheet.Cells[headerRowIndex, c].Value?.ToString()?.Trim() ?? "";
            
            if (string.IsNullOrEmpty(headerText) || 
                headerText == "!!!!" || 
                System.Text.RegularExpressions.Regex.IsMatch(headerText, @"^Column\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                
                bool hasData = false;
                for (int r = headerRowIndex + 1; r <= rowCount; r++)
                {
                    var cellValue = sheet.Cells[r, c].Value?.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(cellValue) && cellValue != "!!!!")
                    {
                        hasData = true;
                        break;
                    }
                }
                
                if (!hasData)
                    continue; 
            }
            
            validColumnIndices.Add(c);
            if (string.IsNullOrEmpty(headerText) || headerText == "!!!!")
                headers.Add($"Column{c}");
            else
                headers.Add(headerText);
        }

        for (int r = headerRowIndex + 1; r <= rowCount; r++)
        {
            var row = new Dictionary<string, string>();
            bool hasData = false;

            for (int colIndex = 0; colIndex < validColumnIndices.Count; colIndex++)
            {
                int c = validColumnIndices[colIndex];
                var cellValue = sheet.Cells[r, c].Value?.ToString() ?? string.Empty;
                
                if (cellValue.Trim() != "!!!!")
                    hasData = true;

                row[headers[colIndex]] = cellValue;
            }

            if (hasData)
                rows.Add(row);
        }

        return rows;
    }
    catch (Exception ex)
    {
        throw new Exception($"Error parsing Excel file: {ex.Message}", ex);
    }
}

    private List<Dictionary<string, string>> ParseCsv(IFormFile file)
    {
        var rows = new List<Dictionary<string, string>>();
        
        using var reader = new StreamReader(file.OpenReadStream());
        var line = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(line))
            return rows;

        var headers = line.Split(',').Select(h => h.Trim()).ToList();

        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var values = line.Split(',');
            var row = new Dictionary<string, string>();

            for (int i = 0; i < headers.Count && i < values.Length; i++)
            {
                row[headers[i]] = values[i].Trim();
            }
            rows.Add(row);
        }

        return rows;
    }

    private List<Dictionary<string, string>> ParsePdf(IFormFile file)
    {
        using var pdf = PdfDocument.Open(file.OpenReadStream());

        var text = string.Join("\n", pdf.GetPages().Select(p => p.Text));

        if (string.IsNullOrWhiteSpace(text) || text.Length < 50)
            throw new Exception("Image-only or empty PDF not allowed");

        return text.Split('\n')
                   .Where(l => !string.IsNullOrWhiteSpace(l))
                   .Select(l => new Dictionary<string, string>
                   {
                       { "Content", l.Trim() }
                   })
                   .ToList();
    }
}