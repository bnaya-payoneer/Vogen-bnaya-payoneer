﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using Vogen;
using Vogen.EfCoreTest;

[assembly: VogenDefaults(
    openApiSchemaCustomizations: OpenApiSchemaCustomizations.GenerateSwashbuckleSchemaFilter | OpenApiSchemaCustomizations.GenerateSwashbuckleMappingExtensionMethod,
    staticAbstractsGeneration: StaticAbstractsGeneration.MostCommon | StaticAbstractsGeneration.InstanceMethodsAndProperties, 
    conversions: Conversions.EfCoreValueConverter)]

namespace Testbench;

[ValueObject<short>]
public partial struct MyShort;

[ValueObject<DateOnly>] public partial struct MyDateOnly;
[ValueObject<DateTime>] public partial struct MyDateTime;
[ValueObject<DateTimeOffset>] public partial struct MyDateTimeOffset;
[ValueObject<byte>] public partial struct MyByte;

[ValueObject<long>]
public partial struct MyLong;

public static class Program
{
    public static void Main()
    {

        EfCoreScenario.Run();
    }
}
