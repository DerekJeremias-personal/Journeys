using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using Journeys.DTO.Interfaces;
using Journeys.DTO.Models;

namespace Journeys.DTO.JsonConverters
{
    public class NavigationCriteriaConverter //: JsonConverter<INavigationCriteriaDto>
    {
        //public override INavigationCriteriaDto? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        //{
        //    var jsonObject = JsonDocument.ParseValue(ref reader).RootElement;

        //    // Logic to determine and instantiate the correct implementation
        //    //if (jsonObject.TryGetProperty("Type", out var typeProperty))
        //    //{
        //    //    var type = typeProperty.GetString();
        //    //    switch (type)
        //    //    {
        //    //        case "SimpleNavigationCriteria":
        //    //            return JsonSerializer.Deserialize<SimpleNavigationCriteria>(jsonObject.GetRawText(), options);
        //    //    }
        //    //}

        //    return JsonSerializer.Deserialize<SimpleNavigationCriteriaDto>(jsonObject.GetRawText(), options);
        //}

        //public override void Write(Utf8JsonWriter writer, INavigationCriteriaDto value, JsonSerializerOptions options)
        //{
        //    JsonSerializer.Serialize(writer, (object)value, options);
        //}
    }
}
