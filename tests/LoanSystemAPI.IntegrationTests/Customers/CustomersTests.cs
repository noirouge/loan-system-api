using LoanSystemAPI.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoanSystemAPI.IntegrationTests.Customers
{
    [Collection(ApiCollection.Name)]
    public class CustomersTests : IntegrationTest
    {
        public CustomersTests(ApiFixture fixture) : base(fixture)
        {
        }

        private async Task<Guid> CreateCustomerAsync(string fullname)
        {
            var response = await Client.PostAsJsonAsync("/api/customers", new { fullname, code = "C-1", phone = "809-555-1234", note = "Test" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("id").GetGuid();
        }

        [Fact]
        public async Task Post_CreatesTheCustomer_AndGetByIdReturnsIt()
        {
            var id = await CreateCustomerAsync("Fulanito");

            var customer = await Client.GetFromJsonAsync<JsonElement>($"/api/customers/{id}");

            Assert.Equal("Fulanito", customer.GetProperty("fullname").GetString());
            Assert.Equal("809-555-1234", customer.GetProperty("phone").GetString());
        }

        [Fact]
        public async Task Responses_DoNotExposeAuditFields()
        {
            var id = await CreateCustomerAsync("Fulanito");

            var one = await Client.GetFromJsonAsync<JsonElement>($"/api/customers/{id}");
            var list = await Client.GetFromJsonAsync<JsonElement>("/api/customers");

            foreach (var customer in new[] { one, list[0] })
            {
                Assert.False(customer.TryGetProperty("createdBy", out _));
                Assert.False(customer.TryGetProperty("createdDate", out _));
                Assert.False(customer.TryGetProperty("updatedBy", out _));
                Assert.False(customer.TryGetProperty("updatedDate", out _));
            }
        }

        [Fact]
        public async Task GetAll_ReturnsTheNewestFirst()
        {
            await CreateCustomerAsync("Primero");
            await CreateCustomerAsync("Segundo");

            var list = await Client.GetFromJsonAsync<JsonElement>("/api/customers");

            Assert.Equal(2, list.GetArrayLength());
            Assert.Equal("Segundo", list[0].GetProperty("fullname").GetString());
            Assert.Equal("Primero", list[1].GetProperty("fullname").GetString());
        }

        [Fact]
        public async Task Put_UpdatesTheCustomer()
        {
            var id = await CreateCustomerAsync("Fulanito");

            var response = await Client.PutAsJsonAsync("/api/customers", new { id, fullname = "Fulanito de Tal", code = "C-2", phone = "849-555-9876", note = "Updated" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var customer = await Client.GetFromJsonAsync<JsonElement>($"/api/customers/{id}");
            Assert.Equal("Fulanito de Tal", customer.GetProperty("fullname").GetString());
            Assert.Equal("849-555-9876", customer.GetProperty("phone").GetString());
        }

        [Fact]
        public async Task Delete_HidesTheCustomer()
        {
            var id = await CreateCustomerAsync("Fulanito");

            var response = await Client.DeleteAsync($"/api/customers/{id}");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/customers/{id}")).StatusCode);
            Assert.Equal(0, (await Client.GetFromJsonAsync<JsonElement>("/api/customers")).GetArrayLength());
        }

        [Fact]
        public async Task UnknownCustomer_ReturnsNotFound()
        {
            var id = Guid.NewGuid();

            Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/customers/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Client.DeleteAsync($"/api/customers/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Client.PutAsJsonAsync("/api/customers", new { id, fullname = "Nadie" })).StatusCode);
        }
    }
}
