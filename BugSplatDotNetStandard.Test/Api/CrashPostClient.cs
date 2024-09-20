using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BugSplatDotNetStandard;
using BugSplatDotNetStandard.Api;
using BugSplatDotNetStandard.Http;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Tests.StackTraceFactory;

namespace Tests
{
    [TestFixture]
    public class CrashPostClientTest
    {
        FileInfo lockedFile;
        FileInfo minidumpFile;
        FileStream lockedFileWriter;

        string database;
        string application = "TestApp";
        string version = "1.0.0";
        string clientId;
        string clientSecret;

        [OneTimeSetUp]
        public void SetUp()
        {
            DotNetEnv.Env.Load();
            database = Environment.GetEnvironmentVariable("BUGSPLAT_DATABASE");
            clientId = Environment.GetEnvironmentVariable("BUGSPLAT_CLIENT_ID");
            clientSecret = Environment.GetEnvironmentVariable("BUGSPLAT_CLIENT_SECRET");

            var bytesToWrite = Encoding.UTF8.GetBytes("This file is locked");
            lockedFile = new FileInfo("lockedFile.txt");
            lockedFileWriter = File.Open(lockedFile.FullName, FileMode.Create, FileAccess.Write, FileShare.None);
            lockedFileWriter.Write(bytesToWrite, 0, bytesToWrite.Length);
            lockedFileWriter.Flush();

            minidumpFile = new FileInfo("minidump.dmp");
            File.Create(minidumpFile.FullName).Close();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            lockedFileWriter.Close();
            lockedFile.Delete();
            minidumpFile.Delete();
        }

        [Test]
        public void CrashPostClient_Constructor_ShouldThrowIfHttpClientFactoryIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new CrashPostClient(null, S3ClientFactory.Default));
        }

        [Test]
        public void CrashPostClient_Constructor_ShouldThrowIfS3ClientFactoryIsNullOrEmpty()
        {
            Assert.Throws<ArgumentNullException>(() => new CrashPostClient(HttpClientFactory.Default, null));
        }

        [Test]
        public async Task CrashPostClient_PostException_ShouldReturn200()
        {
            var stackTrace = CreateStackTrace();
            var bugsplat = new BugSplat(database, application, version);
            var getCrashUrl = "https://fake.url.com";
            var mockHttp = CreateMockHttpClientForExceptionPost(getCrashUrl);
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var mockS3ClientFactory = FakeS3ClientFactory.CreateMockS3ClientFactory();

            var sut = new CrashPostClient(httpClientFactory, mockS3ClientFactory);

            var postResult = await sut.PostException(
                database,
                application,
                version,
                stackTrace,
                ExceptionPostOptions.Create(bugsplat)
            );

            Assert.AreEqual(HttpStatusCode.OK, postResult.StatusCode);
        }

        [Test]
        public async Task CrashPostClient_PostMinidump_ShouldReturn200()
        {
            var bugsplat = new BugSplat(database, application, version);
            var getCrashUrl = "https://fake.url.com";
            var mockHttp = CreateMockHttpClientForExceptionPost(getCrashUrl);
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var mockS3ClientFactory = FakeS3ClientFactory.CreateMockS3ClientFactory();

            var sut = new CrashPostClient(httpClientFactory, mockS3ClientFactory);

            var postResult = await sut.PostMinidump(
                database,
                application,
                version,
                new FileInfo("Files/minidump.dmp"),
                MinidumpPostOptions.Create(bugsplat)
            );

            Assert.AreEqual(HttpStatusCode.OK, postResult.StatusCode);
        }

        [Test]
        public void CrashPostClient_PostException_ShouldNotThrowIfAttachmentLocked()
        {
            var stackTrace = CreateStackTrace();
            var bugsplat = new BugSplat(database, application, version);
            bugsplat.Attachments.Add(lockedFile);
            var getCrashUrl = "https://fake.url.com";
            var mockHttp = CreateMockHttpClientForExceptionPost(getCrashUrl);
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var mockS3ClientFactory = FakeS3ClientFactory.CreateMockS3ClientFactory();

            var sut = new CrashPostClient(httpClientFactory, mockS3ClientFactory);

            Assert.DoesNotThrowAsync(async () =>
            {
                await sut.PostException(
                    database,
                    application,
                    version,
                    stackTrace,
                    ExceptionPostOptions.Create(bugsplat)
                );
            });
        }

        [Test]
        public void CrashPostClient_PostCrashFile_ShouldNotThrowIfAttachmentLocked()
        {
            var bugsplat = new BugSplat(database, application, version);
            bugsplat.Attachments.Add(lockedFile);
            var getCrashUrl = "https://fake.url.com";
            var mockHttp = CreateMockHttpClientForExceptionPost(getCrashUrl);
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var mockS3ClientFactory = FakeS3ClientFactory.CreateMockS3ClientFactory();

            var sut = new CrashPostClient(httpClientFactory, mockS3ClientFactory);

            Assert.DoesNotThrowAsync(async () =>
            {
                await sut.PostCrashFile(
                    database,
                    application,
                    version,
                    new FileInfo("minidump.dmp"),
                    MinidumpPostOptions.Create(bugsplat)
                );
            });
        }

        [Test]
        [Explicit]
        public async Task CrashPostClient_PostCrashFile_PostCrashAndMetadata()
        {
            var stackTrace = CreateStackTrace();
            var bugsplat = new BugSplat(database, application, version);
            bugsplat.Description = "Test description";
            bugsplat.Email = "test@test.com";
            bugsplat.Key = "Test key";
            bugsplat.Notes = "Test notes";
            bugsplat.User = "Test user";
            var oauth2ApiClient = OAuth2ApiClient.Create(clientId, clientSecret)
                .Authenticate()
                .Result;
            var crashDetailsClient = CrashDetailsClient.Create(oauth2ApiClient);
            var sut = new CrashPostClient(HttpClientFactory.Default, S3ClientFactory.Default);
            
            var postResult = await sut.PostException(
                database,
                application,
                version,
                stackTrace,
                ExceptionPostOptions.Create(bugsplat)
            );
            var postResponseContent = JObject.Parse(postResult.Content.ReadAsStringAsync().Result);
            var id = postResponseContent["crashId"].Value<int>();
            
            var crashDetails = await crashDetailsClient.GetCrashDetails(database, id);
            var crashDetailsContent = JObject.Parse(crashDetails.Content.ReadAsStringAsync().Result);
            Assert.AreEqual(HttpStatusCode.OK, postResult.StatusCode);
            Assert.AreEqual(bugsplat.Description, crashDetailsContent["description"].Value<string>());
            Assert.AreEqual(bugsplat.Email, crashDetailsContent["email"].Value<string>());
            Assert.AreEqual(bugsplat.Key, crashDetailsContent["appKey"].Value<string>());
            Assert.AreEqual(bugsplat.Notes, crashDetailsContent["comments"].Value<string>());
            Assert.AreEqual(bugsplat.User, crashDetailsContent["user"].Value<string>());
        }

        private Mock<HttpMessageHandler> CreateMockHttpClientForExceptionPost(string crashUploadUrl)
        {
            var getCrashUploadUrlResponse = new HttpResponseMessage();
            getCrashUploadUrlResponse.StatusCode = HttpStatusCode.OK;
            getCrashUploadUrlResponse.Content = new StringContent($"{{ \"url\": \"{crashUploadUrl}\" }}");

            var commitCrashUploadUrlReponse = new HttpResponseMessage();
            commitCrashUploadUrlReponse.StatusCode = HttpStatusCode.OK;

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .SetupSequence<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(getCrashUploadUrlResponse)
               .ReturnsAsync(commitCrashUploadUrlReponse);

            return handlerMock;
        }
    }
}
