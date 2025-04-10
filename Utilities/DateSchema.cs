using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public class DateOnlySchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(DateOnly))
        {
            schema.Type = "string";
            schema.Format = "date";
            schema.Example = new OpenApiString("2025-04-15");
        }

        if (context.Type == typeof(TimeOnly))
        {
            schema.Type = "string";
            schema.Format = "time";
            schema.Example = new OpenApiString("14:30:00");
        }
    }
}
