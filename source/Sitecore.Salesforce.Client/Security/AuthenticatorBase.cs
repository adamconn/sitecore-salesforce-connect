namespace Sitecore.Salesforce.Client.Security
{
  using System;
  using System.Collections.Generic;
  using System.Net.Http;
  using System.Net.Http.Headers;
  using System.Threading.Tasks;

  using Newtonsoft.Json;

  using Sitecore.Salesforce.Client.Data.Errors;
  using Sitecore.Salesforce.Client.Exceptions;
  using System.Net;

    public abstract class AuthenticatorBase : ClientBase, IAuthenticator
  {
    private string tokenRequestUri;

    public string TokenRequestUri
    {
      get
      {
        return this.tokenRequestUri ?? "https://login.salesforce.com/services/oauth2/token";
      }

      set
      {
        this.tokenRequestUri = value;
      }
    }

    protected AuthenticatorBase(HttpClient httpClient)
      : base(httpClient)
    {
    }

    public virtual IAuthToken Authenticate()
    {
      try
      {
        return this.AuthenticateAsync().Result;
      }
      catch (AggregateException ex)
      {
        throw ex.InnerException;
      }
    }

    public virtual async Task<IAuthToken> AuthenticateAsync()
    {
      ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

      HttpContent content = new FormUrlEncodedContent(this.GetAuthParameters());
      
      var request = new HttpRequestMessage
        {
          Method = HttpMethod.Post,
          RequestUri = new Uri(this.TokenRequestUri),
          Content = content
        };

      var responseMessage = await this.HttpClient.SendAsync(request).ConfigureAwait(false);
      var response = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

      if (!responseMessage.IsSuccessStatusCode)
      {
        IErrorResponse error = JsonConvert.DeserializeObject<AuthErrorResponse>(response);

        throw new SalesforceAuthException(error.Message)
          {
            ErrorCode = error.ErrorCode
          };
      }

      return JsonConvert.DeserializeObject<AuthToken>(response);
    }

    protected abstract Dictionary<string, string> GetAuthParameters();
  }
}