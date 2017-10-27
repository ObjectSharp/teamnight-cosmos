using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CosmosSpeedTestApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Options;

namespace CosmosSpeedTestApi.Controllers
{
    [Route("api/[controller]")]
    public class PixelController : Controller
    {
        const string Database = "Catalog";
        const string Collection = "Products";

        private readonly SpeedTestOptions _options;
        private readonly IDocumentClient _client;
        private readonly IHostingEnvironment _env;

        private Uri _collectionUri;

        public PixelController(IHostingEnvironment env, IOptions<SpeedTestOptions> options, IDocumentClient client)
        {
            _options = options.Value;
            _client = client;
            _env = env;

            _collectionUri = UriFactory.CreateDocumentCollectionUri(Database, Collection);
        }

        [Route("settings")]
        public IActionResult Settings()
        {
            var response = new { _env.EnvironmentName, _options.PreferredLocations };
            return Ok(response);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Pixel pixel)
        {
            var sw = Stopwatch.StartNew();

            if (pixel == null)
                return BadRequest();

            if (string.IsNullOrWhiteSpace(pixel.name))
                return BadRequest("Name is required");

            var response = await _client.CreateDocumentAsync(_collectionUri, pixel);
            sw.Stop();
            Console.WriteLine($"Insert took {sw.ElapsedMilliseconds} ms");

            var result = new { ms = sw.ElapsedMilliseconds, data = response.Resource };

            return Ok(result);
        }

        [HttpPost]
        [Route("bulk")]
        public IActionResult PostBulk([FromBody] Pixel[] pixels, bool assignId = false)
        {
            if (pixels == null)
                return BadRequest();

            List<object> resources = new List<object>();

            var sw = Stopwatch.StartNew();
            var loop = Parallel.ForEach(pixels, (pixel) =>
            {
                if (!string.IsNullOrWhiteSpace(pixel.name))
                {
                    if (assignId)
                        pixel.id = Guid.NewGuid().ToString();

                    var response = _client.CreateDocumentAsync(_collectionUri, pixel).ConfigureAwait(false).GetAwaiter().GetResult();
                    resources.Add(response.Resource);
                }
            });
            sw.Stop();
            Console.WriteLine($"Inserted {pixels.Length} in {sw.ElapsedMilliseconds} ms");
            var result = new { ms = sw.ElapsedMilliseconds, data = resources };
            return Ok(result);
        }

        [HttpPost]
        [Route("bulk2")]
        public async Task<IActionResult> PostBulk2([FromBody] Pixel[] pixels, bool assignId = false)
        {
            if (pixels == null)
                return BadRequest();

            if (pixels.Select(p => p.name).Distinct().Count() > 1)
                return BadRequest();

            string name = pixels[0].name;
            string transactionId = Guid.NewGuid().ToString();

            var sw = Stopwatch.StartNew();
            long insertCount = 0;
            while (insertCount < pixels.Length)
            {
                var payload = insertCount == 0 ? pixels.OrderBy(p => p.seq).ToArray() : pixels.OrderBy(p => p.seq).Skip((int)insertCount).ToArray();
                Console.WriteLine("Uploading...");
                var response = await _client.ExecuteStoredProcedureAsync<int>(
                    UriFactory.CreateStoredProcedureUri(Database, Collection, "bulkInsert"),
                    new RequestOptions { PartitionKey = new PartitionKey(name) },
                    transactionId,
                    payload
                );
                insertCount += response.Response;
                Console.WriteLine($"Uploaded {insertCount} documents");
            }
            sw.Stop();
            Console.WriteLine($"Inserted {insertCount} in {sw.ElapsedMilliseconds} ms");
            var result = new { ms = sw.ElapsedMilliseconds, count = insertCount, seqs = pixels.Select(p => new { p.x, p.y }).ToArray() };
            return Ok(result);
        }

        [Route("count/{name}")]
        public IActionResult Count(string name)
        {
            var sw = Stopwatch.StartNew();
            SqlQuerySpec query = null;
            if (string.IsNullOrWhiteSpace(name) || name.Equals("*"))
            {
                query = new SqlQuerySpec("SELECT VALUE COUNT(f) FROM Products f");
            }
            else
            {
                query = new SqlQuerySpec("SELECT VALUE COUNT(f) FROM Products f WHERE f.name = @name")
                {
                    Parameters = new SqlParameterCollection { new SqlParameter("@name", name) }
                };
            }

            int count = _client.CreateDocumentQuery<int>(_collectionUri, query, new FeedOptions { EnableCrossPartitionQuery = true }).AsEnumerable().FirstOrDefault();

            sw.Stop();
            Console.WriteLine($"Query took {sw.ElapsedMilliseconds} ms");

            return Ok(new { count = count, ms = sw.ElapsedMilliseconds });
        }

        [Route("copy/{source}")]
        [HttpPost]
        public async Task<IActionResult> Copy(string source, [FromBody]Copy copy )
        {
            var sw = Stopwatch.StartNew();
            // SQL
            var query = new SqlQuerySpec("SELECT * FROM Products f WHERE f.name = @name")
            {
                Parameters = new SqlParameterCollection { new SqlParameter("@name", source) }
            };

            var sourceDocs = _client.CreateDocumentQuery<Pixel>(_collectionUri, query, new FeedOptions { EnableCrossPartitionQuery = true }).ToArray();
            foreach (var d in sourceDocs)
            {
                d.id = Guid.NewGuid().ToString();
                d.name = copy.destination;
            };
            sw.Stop();
            Console.WriteLine($"Retrieved {sourceDocs.Length} source documents in {sw.ElapsedMilliseconds} ms");
            sw.Restart();
            await PostBulk2(sourceDocs);
            sw.Stop();
            Console.WriteLine($"Copied {sourceDocs.Length} source documents in {sw.ElapsedMilliseconds} ms");
            return Ok(new { count = sourceDocs.Length, ms = sw.ElapsedMilliseconds });
        }

        [Route("{name}")]
        [HttpDelete]
        public async Task<IActionResult> Delete(string name)
        {
            var sw = Stopwatch.StartNew();
            var query = new SqlQuerySpec("SELECT p.id FROM Products p WHERE p.name = @name")
            {
                Parameters = new SqlParameterCollection { new SqlParameter("@name", name) }
            };

            var result = _client.CreateDocumentQuery<PixelKey>(_collectionUri, query, new FeedOptions { EnableCrossPartitionQuery = true }).ToList();
            int count = 0;
            foreach (var pixel in result)
            {
                var response = await _client.DeleteDocumentAsync(
                    UriFactory.CreateDocumentUri(Database, Collection, pixel.id),
                    new RequestOptions { PartitionKey = new PartitionKey(name) }
                );
                count++;
            }

            sw.Stop();
            Console.WriteLine($"Query took {sw.ElapsedMilliseconds} ms");

            return Ok(new { count = count, ms = sw.ElapsedMilliseconds });
        }

        [Route("{name}/nuke")]
        [HttpDelete]
        public async Task<IActionResult> Nuke(string name, bool deleteAll = false)
        {
            bool moreToDelete = true;
            int deleteCount = 0;
            long ticks = 0;
            while (moreToDelete)
            {
                var sw = Stopwatch.StartNew();
                var response = await _client.ExecuteStoredProcedureAsync<BulkDelete>(
                    UriFactory.CreateStoredProcedureUri(Database, Collection, "bulkDelete"),
                    new RequestOptions { PartitionKey = new PartitionKey(name) },
                    $"SELECT * FROM Products p WHERE p.name = '{name}'"
                );
                sw.Stop();
                Console.WriteLine($"Query took {sw.ElapsedMilliseconds} ms, count = {response.Response.deleted}, moreToDelete={response.Response.continuation}");
                deleteCount += response.Response.deleted;
                moreToDelete = response.Response.continuation;
                ticks += sw.ElapsedMilliseconds;
            }
            
            return Ok(new { ms = ticks, count = deleteCount, moreToDelete = moreToDelete });
        }

        [Route("{name}")]
        public IActionResult Get(string name, int? seq = null)
        {
            var sw = Stopwatch.StartNew();
            SqlQuerySpec query = null;
            if (seq.HasValue)
            {
                query = new SqlQuerySpec("SELECT * FROM Products p WHERE p.name = @name AND p.seq > @seq")
                {
                    Parameters = new SqlParameterCollection { new SqlParameter("@name", name), new SqlParameter("@seq", seq.Value )}
                };
            }
            else
            {
                query = new SqlQuerySpec("SELECT * FROM Products p WHERE p.name = @name")
                {
                    Parameters = new SqlParameterCollection { new SqlParameter("@name", name) }
                };
            }

            var result = _client.CreateDocumentQuery<Pixel>(_collectionUri, query, new FeedOptions { EnableCrossPartitionQuery = true }).ToList();
            
            sw.Stop();
            Console.WriteLine($"Query took {sw.ElapsedMilliseconds} ms");

            return Ok(new { count = result.Count, data = result, ms = sw.ElapsedMilliseconds });
        }
    }

    public class PixelKey
    {
        public string id { get; set; }
    }

    public class BulkDelete
    {
        public int deleted { get; set; }
        public bool continuation { get; set; }
    }

    public class Copy
    {
        public string destination { get; set; }
        public bool randomizeRegions { get; set; }
    }
}
