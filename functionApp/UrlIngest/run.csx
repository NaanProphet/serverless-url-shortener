#load ".\models.csx"
#r "Microsoft.WindowsAzure.Storage"
using Microsoft.WindowsAzure.Storage.Table;
using System.Net;
using System;
using System.Linq;
using System.Text;
using System.Web;

public static readonly string SHORTENER_URL = System.Environment.GetEnvironmentVariable("SHORTENER_URL");
public static readonly string UTM_SOURCE = System.Environment.GetEnvironmentVariable("UTM_SOURCE");
public static readonly string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

public static string GenerateCoupon(int length) {
  Random random = new Random();
  StringBuilder result = new StringBuilder(length);
  for (int i = 0; i < length; i++) {
    result.Append(Alphabet[random.Next(Alphabet.Length)]);
  }
  return result.ToString();
}

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, NextId keyTable, CloudTable tableOut, TraceWriter log)
{
    log.Info($"C# manually triggered function called with req: {req}");

    if (req == null)
    {
        return req.CreateResponse(HttpStatusCode.NotFound);
    }

    Request input = await req.Content.ReadAsAsync<Request>();

    if (input == null)
    {
        return req.CreateResponse(HttpStatusCode.NotFound);
    }

    var result = new List<Result>();
    var url = input.Input;
    bool tagMediums = input.TagMediums.HasValue ? input.TagMediums.Value : true;
    bool tagSource = (input.TagSource.HasValue ? input.TagSource.Value : true) || tagMediums;

    log.Info($"URL: {url} Tag Source? {tagSource} Tag Mediums? {tagMediums}");
    
    if (String.IsNullOrWhiteSpace(url))
    {
        throw new Exception("Need a URL to shorten!");
    }

    if (keyTable == null)
    {
        keyTable = new NextId
        {
            PartitionKey = "1",
            RowKey = "KEY",
            Id = 1024
        };
        var keyAdd = TableOperation.Insert(keyTable);
        await tableOut.ExecuteAsync(keyAdd); 
    }
    
    log.Info($"Current key: {keyTable.Id}"); 

    for (int i=0; i < 100; i++)
    {
        var shortUrl = GenerateCoupon(10);
        log.Info($"Short URL for {url} is {shortUrl}");
        var newUrl = new ShortUrl 
        {
            PartitionKey = $"{shortUrl.First()}",
            RowKey = $"{shortUrl}",
            Url = url
        };
        var singleAdd = TableOperation.Insert(newUrl);
        await tableOut.ExecuteAsync(singleAdd);
        result.Add(new Result 
        {
            ShortUrl = $"{SHORTENER_URL}{newUrl.RowKey}",
            LongUrl = WebUtility.UrlDecode(newUrl.Url)
        }); 
    }

    var operation = TableOperation.Replace(keyTable);
    await tableOut.ExecuteAsync(operation);

    log.Info($"Done.");
    return req.CreateResponse(HttpStatusCode.OK, result);
    
}
