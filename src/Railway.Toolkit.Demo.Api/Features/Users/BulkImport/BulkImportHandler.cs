using Railway.Toolkit.Demo.Api.Features.Users.Register;

namespace Railway.Toolkit.Demo.Api.Features.Users.BulkImport;

public sealed class BulkImportHandler
{
    private readonly RegisterHandler _registerHandler;

    public BulkImportHandler(RegisterHandler registerHandler)
    {
        _registerHandler = registerHandler;
    }

    public Result<ImportResponse> Handle(string csvContent)
    {
        string[] lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Need at least a header row and one data row
        if (lines.Length < 2)
        {
            return Result.Fail<ImportResponse>("CSV file contains no data rows", "Import.NoData");
        }

        // Step off the railway — batch loop where partial failure is expected
        List<ImportRowResult> results = new();

        for (int i = 1; i < lines.Length; i++)
        {
            int rowNumber = i;
            string line = lines[i];

            Result<RegisterRequest> parseResult = ParseRow(line);
            ImportRowResult rowResult;

            if (parseResult is Result<RegisterRequest>.Ok ok)
            {
                Result<RegisterResponse> registerResult = _registerHandler.Handle(ok.Value);

                if (registerResult is Result<RegisterResponse>.Ok registered)
                {
                    rowResult = new ImportRowResult
                    {
                        Row = rowNumber,
                        Success = true,
                        UserId = registered.Value.Id,
                        Username = registered.Value.Username
                    };
                }
                else
                {
                    Result<RegisterResponse>.Fail fail = (Result<RegisterResponse>.Fail)registerResult;
                    rowResult = new ImportRowResult
                    {
                        Row = rowNumber,
                        Success = false,
                        Error = fail.Error
                    };
                }
            }
            else
            {
                Result<RegisterRequest>.Fail fail = (Result<RegisterRequest>.Fail)parseResult;
                rowResult = new ImportRowResult
                {
                    Row = rowNumber,
                    Success = false,
                    Error = fail.Error
                };
            }

            results.Add(rowResult);
        }

        if (results.Count == 0)
        {
            return Result.Fail<ImportResponse>("CSV file contains no data rows", "Import.NoData");
        }

        int succeeded = 0;
        int failed = 0;

        foreach (ImportRowResult row in results)
        {
            if (row.Success)
            {
                succeeded++;
            }
            else
            {
                failed++;
            }
        }

        // Step back onto the railway — batch complete, return summary as Ok
        return Result.Ok(new ImportResponse
        {
            TotalRows = results.Count,
            Succeeded = succeeded,
            Failed = failed,
            Results = results
        });
    }

    private static Result<RegisterRequest> ParseRow(string line)
    {
        string[] parts = line.Split(',');

        if (parts.Length != 4)
        {
            return Result.Fail<RegisterRequest>(
                $"Expected 4 columns (Username,Email,Password,DateOfBirth) but found {parts.Length}",
                "Import.InvalidFormat");
        }

        string username = parts[0].Trim();
        string email = parts[1].Trim();
        string password = parts[2].Trim();
        string dateOfBirthStr = parts[3].Trim();

        if (!DateOnly.TryParse(dateOfBirthStr, out DateOnly dateOfBirth))
        {
            return Result.Fail<RegisterRequest>(
                $"Invalid DateOfBirth '{dateOfBirthStr}' — expected yyyy-MM-dd",
                "Import.InvalidDate");
        }

        return Result.Ok(new RegisterRequest
        {
            Username = username,
            Email = email,
            Password = password,
            DateOfBirth = dateOfBirth
        });
    }
}
