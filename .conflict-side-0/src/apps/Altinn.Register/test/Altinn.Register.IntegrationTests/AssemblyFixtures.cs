﻿using Altinn.Register.IntegrationTests.Fixtures;
using Altinn.Register.TestUtils.Database;
using Altinn.Register.TestUtils.Traits;

[assembly: IntegrationTest]
[assembly: AssemblyFixture(typeof(PostgresServerFixture))]
[assembly: AssemblyFixture(typeof(WebApplicationFixture))]
