﻿using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Commons.API.Auth.Authentication.Features.AuthenticationFeature;
using Commons.API.Auth.Authentication.Features.RawAuthenticationFeature;
using Commons.API.Auth.Authentication.Tokens.JWT;

namespace Commons.API.Auth.Authentication
{
    public class AuthenticationMiddleware<TIdentity> : IMiddleware where TIdentity : class, new()
    {
        private readonly JwtAuthentication<TIdentity> jwtAuthentication;

        public AuthenticationMiddleware(JwtAuthentication<TIdentity> jwtAuthentication)
        {
            this.jwtAuthentication = jwtAuthentication;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var endpoint = context.Features.Get<IEndpointFeature>()?.Endpoint;
            var attribute = endpoint?.Metadata.GetMetadata<AuthenticateAttribute>();
            if (attribute is null)
            {
                await next(context);
                return;
            }

            var input = context.Features.Get<ITokenFeature>();
            if (input is null)
            {
                await next(context);
                return;
            }

            var jwtToken = input.AccessToken;
            if (string.IsNullOrWhiteSpace(jwtToken))
            {
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    Message = "Access token was not provided in \"access-token\" HTTP Cookie."
                }, new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }, context.RequestAborted);
                return;
            }

            var identity = await jwtAuthentication.ValidateJwt(jwtToken);

            if (identity is null)
            {
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    Message = "Access token was invalid."
                }, new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }, context.RequestAborted);

                return;
            }

            var authenticationFeature = new IdentityFeature<TIdentity>()
            {
                Identity = identity
            };
            context.Features.Set<IIdentityFeature<TIdentity>>(authenticationFeature);
            await next(context);
        }
    }
}
