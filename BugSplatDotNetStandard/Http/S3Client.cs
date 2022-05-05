using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace BugSplatDotNetStandard.Http
{
    internal interface IS3ClientFactory
    {
        S3Client Create();
    }

    internal class S3ClientFactory : IS3ClientFactory
    {
        public static IS3ClientFactory Default = new S3ClientFactory();

        public S3Client Create()
        {
            return new S3Client(HttpClientFactory.Default);
        }
    }

    internal class S3Client: IDisposable
    {
        private HttpClient httpClient;

        public S3Client (IHttpClientFactory factory)
        {
            this.httpClient = factory.CreateClient();
        }
        public async Task<HttpResponseMessage> UploadFileBytesToPresignedURL(Uri uri, byte[] bytes)
        {
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            return await httpClient.PutAsync(uri, new ByteArrayContent(bytes));
        }

        public async Task<HttpResponseMessage> UploadFileStreamToPresignedURL(Uri uri, FileStream fileStream)
        {
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            return await httpClient.PutAsync(uri, new StreamContent(fileStream));
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
        }
    }
}