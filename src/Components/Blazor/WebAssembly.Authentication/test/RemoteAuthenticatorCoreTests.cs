using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Components.WebAssembly.Authentication
{
    public class RemoteAuthenticatorCoreTests
    {
        private const string _action = nameof(RemoteAuthenticatorViewCore<RemoteAuthenticationState>.Action);

        [Fact]
        public async Task AuthenticationManager_Throws_ForInvalidAction()
        {
            // Arrange
            var remoteAuthenticator = new RemoteAuthenticatorViewCore<RemoteAuthenticationState>();

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [_action] = ""
            });

            // Act & assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => remoteAuthenticator.SetParametersAsync(parameters));
        }

        [Fact]
        public async Task AuthenticationManager_Login_NavigatesToReturnUrlOnSuccess()
        {
            // Arrange
            var (remoteAuthenticator, renderer, authServiceMock, jsRuntime) = CreateAuthenticationManager(
                "https://www.example.com/base/authentication/login?returnUrl=https://www.example.com/base/fetchData");

            authServiceMock.Setup(s => s.SignInAsync(It.IsAny<RemoteAuthenticationContext<RemoteAuthenticationState>>()))
                .ReturnsAsync(new RemoteAuthenticationResult<RemoteAuthenticationState>()
                {
                    Status = RemoteAuthenticationStatus.Success,
                    State = remoteAuthenticator.AuthenticationState
                });

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [_action] = RemoteAuthenticationActions.LogIn
            });

            // Act
            await renderer.Dispatcher.InvokeAsync<object>(() => remoteAuthenticator.SetParametersAsync(parameters));

            // Assert
            Assert.Equal("https://www.example.com/base/fetchData", remoteAuthenticator.Navigation.Uri);
            authServiceMock.Verify();
        }

        [Fact]
        public async Task AuthenticationManager_Login_DoesNothingOnRedirect()
        {
            // Arrange
            var originalUrl = "https://www.example.com/base/authentication/login?returnUrl=https://www.example.com/base/fetchData";
            var (remoteAuthenticator, renderer, authServiceMock, jsRuntime) = CreateAuthenticationManager(originalUrl);

            authServiceMock.Setup(s => s.SignInAsync(It.IsAny<RemoteAuthenticationContext<RemoteAuthenticationState>>()))
                .ReturnsAsync(new RemoteAuthenticationResult<RemoteAuthenticationState>()
                {
                    Status = RemoteAuthenticationStatus.Redirect,
                    State = remoteAuthenticator.AuthenticationState
                });

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [_action] = RemoteAuthenticationActions.LogIn
            });

            // Act
            await renderer.Dispatcher.InvokeAsync<object>(() => remoteAuthenticator.SetParametersAsync(parameters));

            // Assert
            Assert.Equal(originalUrl, remoteAuthenticator.Navigation.Uri);

            authServiceMock.Verify();
        }

        [Fact]
        public async Task AuthenticationManager_Login_NavigatesToLoginFailureOnError()
        {
            // Arrange
            var (remoteAuthenticator, renderer, authServiceMock, jsRuntime) = CreateAuthenticationManager(
                "https://www.example.com/base/authentication/login?returnUrl=https://www.example.com/base/fetchData");

            authServiceMock.Setup(s => s.SignInAsync(It.IsAny<RemoteAuthenticationContext<RemoteAuthenticationState>>()))
                .ReturnsAsync(new RemoteAuthenticationResult<RemoteAuthenticationState>()
                {
                    Status = RemoteAuthenticationStatus.Failure,
                    ErrorMessage = "There was an error trying to log in"
                });

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [_action] = RemoteAuthenticationActions.LogIn
            });

            // Act
            await renderer.Dispatcher.InvokeAsync<object>(() => remoteAuthenticator.SetParametersAsync(parameters));

            // Assert
            Assert.Equal(
                "https://www.example.com/base/authentication/login-failed?message=There was an error trying to log in",
                remoteAuthenticator.Navigation.Uri);

            authServiceMock.Verify();
        }

        [Fact]
        public async Task AuthenticationManager_LoginCallback_ThrowsOnRedirectResult()
        {
            // Arrange
            var (remoteAuthenticator, renderer, authServiceMock, jsRuntime) = CreateAuthenticationManager(
                "https://www.example.com/base/authentication/login?returnUrl=https://www.example.com/base/fetchData");

            authServiceMock.Setup(s => s.CompleteSignInAsync(It.IsAny<RemoteAuthenticationContext<RemoteAuthenticationState>>()))
                .ReturnsAsync(new RemoteAuthenticationResult<RemoteAuthenticationState>()
                {
                    Status = RemoteAuthenticationStatus.Redirect
                });

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [_action] = RemoteAuthenticationActions.LogInCallback
            });

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await renderer.Dispatcher.InvokeAsync<object>(async () =>
                {
                    await remoteAuthenticator.SetParametersAsync(parameters);
                    return null;
                }));
        }

        [Fact]
        public async Task AuthenticationManager_LoginCallback_DoesNothingOnOperationCompleted()
        {
            // Arrange
            var originalUrl = "https://www.example.com/base/authentication/login-callback?code=1234";
            var (remoteAuthenticator, renderer, authServiceMock, jsRuntime) = CreateAuthenticationManager(
                originalUrl);

            authServiceMock.Setup(s => s.CompleteSignInAsync(It.IsAny<RemoteAuthenticationContext<RemoteAuthenticationState>>()))
                .ReturnsAsync(new RemoteAuthenticationResult<RemoteAuthenticationState>()
                {
                    Status = RemoteAuthenticationStatus.OperationCompleted
                });

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [_action] = RemoteAuthenticationActions.LogInCallback
            });

            // Act
            await renderer.Dispatcher.InvokeAsync<object>(() => remoteAuthenticator.SetParametersAsync(parameters));

            // Assert
            Assert.Equal(originalUrl, remoteAuthenticator.Navigation.Uri);

            authServiceMock.Verify();
        }

        [Fact]
        public async Task AuthenticationManager_LoginCallback_NavigatesToReturnUrlFromStateOnSuccess()
        {
            // Arrange
            var (remoteAuthenticator, renderer, authServiceMock, jsRuntime) = CreateAuthenticationManager(
                "https://www.example.com/base/authentication/login-callback?code=1234");

            var fetchDataUrl = "https://www.example.com/base/fetchData";
            remoteAuthenticator.AuthenticationState.ReturnUrl = fetchDataUrl;

            authServiceMock.Setup(s => s.CompleteSignInAsync(It.IsAny<RemoteAuthenticationContext<RemoteAuthenticationState>>()))
                .ReturnsAsync(new RemoteAuthenticationResult<RemoteAuthenticationState>()
                {
                    Status = RemoteAuthenticationStatus.Success,
                    State = remoteAuthenticator.AuthenticationState
                });

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [_action] = RemoteAuthenticationActions.LogInCallback
            });

            // Act
            await renderer.Dispatcher.InvokeAsync<object>(() => remoteAuthenticator.SetParametersAsync(parameters));

            // Assert
            Assert.Equal(fetchDataUrl, jsRuntime.LastInvocation.args[0]);

            authServiceMock.Verify();
        }

        [Fact]
        public async Task AuthenticationManager_LoginCallback_NavigatesToLoginFailureOnError()
        {
            // Arrange
            var (remoteAuthenticator, renderer, authServiceMock, jsRuntime) = CreateAuthenticationManager(
                "https://www.example.com/base/authentication/login-callback?code=1234");

            var fetchDataUrl = "https://www.example.com/base/fetchData";
            remoteAuthenticator.AuthenticationState.ReturnUrl = fetchDataUrl;

            authServiceMock.Setup(s => s.CompleteSignInAsync(It.IsAny<RemoteAuthenticationContext<RemoteAuthenticationState>>()))
                .ReturnsAsync(new RemoteAuthenticationResult<RemoteAuthenticationState>()
                {
                    Status = RemoteAuthenticationStatus.Failure,
                    ErrorMessage = "There was an error trying to log in"
                });

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [_action] = RemoteAuthenticationActions.LogInCallback
            });

            // Act
            await renderer.Dispatcher.InvokeAsync<object>(() => remoteAuthenticator.SetParametersAsync(parameters));

            // Assert
            Assert.Equal(
                "https://www.example.com/base/authentication/login-failed?message=There was an error trying to log in",
                remoteAuthenticator.Navigation.Uri);

            authServiceMock.Verify();
        }

        [Fact]
        public async Task AuthenticationManager_Logout_NavigatesToReturnUrlOnSuccess()
        {
            // Arrange
            var (remoteAuthenticator, renderer, authServiceMock, jsRuntime) = CreateAuthenticationManager(
                "https://www.example.com/base/authentication/logout?returnUrl=https://www.example.com/base/");

            authServiceMock.Setup(s => s.GetCurrentUser())
                .ReturnsAsync(new ClaimsPrincipal(new ClaimsIdentity("Test")));

            authServiceMock.Setup(s => s.SignOutAsync(It.IsAny<RemoteAuthenticationContext<RemoteAuthenticationState>>()))
                .ReturnsAsync(new RemoteAuthenticationResult<RemoteAuthenticationState>()
                {
                    Status = RemoteAuthenticationStatus.Success,
                    State = remoteAuthenticator.AuthenticationState
                });

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [_action] = RemoteAuthenticationActions.LogOut
            });

            // Act
            await renderer.Dispatcher.InvokeAsync<object>(() => remoteAuthenticator.SetParametersAsync(parameters));

            // Assert
            Assert.Equal("https://www.example.com/base/", jsRuntime.LastInvocation.args[0]);
            authServiceMock.Verify();
        }

        [Fact]
        public async Task AuthenticationManager_Logout_NavigatesToDefaultReturnUrlWhenNoReturnUrlIsPresent()
        {
            // Arrange
            var (remoteAuthenticator, renderer, authServiceMock, jsRuntime) = CreateAuthenticationManager(
                "https://www.example.com/base/authentication/logout");

            authServiceMock.Setup(s => s.GetCurrentUser())
                .ReturnsAsync(new ClaimsPrincipal(new ClaimsIdentity("Test")));

            authServiceMock.Setup(s => s.SignOutAsync(It.IsAny<RemoteAuthenticationContext<RemoteAuthenticationState>>()))
                .ReturnsAsync(new RemoteAuthenticationResult<RemoteAuthenticationState>()
                {
                    Status = RemoteAuthenticationStatus.Success,
                    State = remoteAuthenticator.AuthenticationState
                });

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [_action] = RemoteAuthenticationActions.LogOut
            });

            // Act
            await renderer.Dispatcher.InvokeAsync<object>(() => remoteAuthenticator.SetParametersAsync(parameters));

            // Assert
            Assert.Equal("https://www.example.com/base/authentication/logged-out", jsRuntime.LastInvocation.args[0]);
            authServiceMock.Verify();
        }

        [Fact]
        public async Task AuthenticationManager_Logout_DoesNothingOnRedirect()
        {
            // Arrange
            var originalUrl = "https://www.example.com/base/authentication/login?returnUrl=https://www.example.com/base/fetchData";
            var (remoteAuthenticator, renderer, authServiceMock, jsRuntime) = CreateAuthenticationManager(originalUrl);

            authServiceMock.Setup(s => s.GetCurrentUser())
                .ReturnsAsync(new ClaimsPrincipal(new ClaimsIdentity("Test")));

            authServiceMock.Setup(s => s.SignOutAsync(It.IsAny<RemoteAuthenticationContext<RemoteAuthenticationState>>()))
                .ReturnsAsync(new RemoteAuthenticationResult<RemoteAuthenticationState>()
                {
                    Status = RemoteAuthenticationStatus.Redirect,
                    State = remoteAuthenticator.AuthenticationState
                });

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [_action] = RemoteAuthenticationActions.LogOut
            });

            // Act
            await renderer.Dispatcher.InvokeAsync<object>(() => remoteAuthenticator.SetParametersAsync(parameters));

            // Assert
            Assert.Equal(originalUrl, remoteAuthenticator.Navigation.Uri);

            authServiceMock.Verify();
        }

        [Fact]
        public async Task AuthenticationManager_Logout_NavigatesToLogoutFailureOnError()
        {
            // Arrange
            var (remoteAuthenticator, renderer, authServiceMock, jsRuntime) = CreateAuthenticationManager(
                "https://www.example.com/base/authentication/logout?returnUrl=https://www.example.com/base/fetchData");

            authServiceMock.Setup(s => s.GetCurrentUser())
                .ReturnsAsync(new ClaimsPrincipal(new ClaimsIdentity("Test")));

            authServiceMock.Setup(s => s.SignOutAsync(It.IsAny<RemoteAuthenticationContext<RemoteAuthenticationState>>()))
                .ReturnsAsync(new RemoteAuthenticationResult<RemoteAuthenticationState>()
                {
                    Status = RemoteAuthenticationStatus.Failure,
                    ErrorMessage = "There was an error trying to log out"
                });

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [_action] = RemoteAuthenticationActions.LogOut
            });

            // Act
            await renderer.Dispatcher.InvokeAsync<object>(() => remoteAuthenticator.SetParametersAsync(parameters));

            // Assert
            Assert.Equal(
                "https://www.example.com/base/authentication/logout-failed?message=There was an error trying to log out",
                remoteAuthenticator.Navigation.Uri);

            authServiceMock.Verify();
        }

        [Fact]
        public async Task AuthenticationManager_LogoutCallback_ThrowsOnRedirectResult()
        {
            // Arrange
            var (remoteAuthenticator, renderer, authServiceMock, jsRuntime) = CreateAuthenticationManager(
                "https://www.example.com/base/authentication/logout-callback?returnUrl=https://www.example.com/base/fetchData");

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [_action] = RemoteAuthenticationActions.LogOutCallback
            });

            authServiceMock.Setup(s => s.CompleteSignOutAsync(It.IsAny<RemoteAuthenticationContext<RemoteAuthenticationState>>()))
                .ReturnsAsync(new RemoteAuthenticationResult<RemoteAuthenticationState>()
                {
                    Status = RemoteAuthenticationStatus.Redirect,
                });


            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await renderer.Dispatcher.InvokeAsync<object>(async () =>
                {
                    await remoteAuthenticator.SetParametersAsync(parameters);
                    return null;
                }));
        }

        [Fact]
        public async Task AuthenticationManager_LogoutCallback_DoesNothingOnOperationCompleted()
        {
            // Arrange
            var originalUrl = "https://www.example.com/base/authentication/logout-callback?code=1234";
            var (remoteAuthenticator, renderer, authServiceMock, jsRuntime) = CreateAuthenticationManager(
                originalUrl);

            authServiceMock.Setup(s => s.CompleteSignOutAsync(It.IsAny<RemoteAuthenticationContext<RemoteAuthenticationState>>()))
                .ReturnsAsync(new RemoteAuthenticationResult<RemoteAuthenticationState>()
                {
                    Status = RemoteAuthenticationStatus.OperationCompleted
                });

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [_action] = RemoteAuthenticationActions.LogOutCallback
            });

            // Act
            await renderer.Dispatcher.InvokeAsync<object>(() => remoteAuthenticator.SetParametersAsync(parameters));

            // Assert
            Assert.Equal(originalUrl, remoteAuthenticator.Navigation.Uri);

            authServiceMock.Verify();
        }

        [Fact]
        public async Task AuthenticationManager_LogoutCallback_NavigatesToReturnUrlFromStateOnSuccess()
        {
            // Arrange
            var (remoteAuthenticator, renderer, authServiceMock, jsRuntime) = CreateAuthenticationManager(
                "https://www.example.com/base/authentication/logout-callback-callback?code=1234");

            var fetchDataUrl = "https://www.example.com/base/fetchData";
            remoteAuthenticator.AuthenticationState.ReturnUrl = fetchDataUrl;

            authServiceMock.Setup(s => s.CompleteSignOutAsync(It.IsAny<RemoteAuthenticationContext<RemoteAuthenticationState>>()))
                .ReturnsAsync(new RemoteAuthenticationResult<RemoteAuthenticationState>()
                {
                    Status = RemoteAuthenticationStatus.Success,
                    State = remoteAuthenticator.AuthenticationState
                });

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [_action] = RemoteAuthenticationActions.LogOutCallback
            });

            // Act
            await renderer.Dispatcher.InvokeAsync<object>(() => remoteAuthenticator.SetParametersAsync(parameters));

            // Assert
            Assert.Equal(fetchDataUrl, jsRuntime.LastInvocation.args[0]);

            authServiceMock.Verify();
        }

        [Fact]
        public async Task AuthenticationManager_LogoutCallback_NavigatesToLoginFailureOnError()
        {
            // Arrange
            var (remoteAuthenticator, renderer, authServiceMock, jsRuntime) = CreateAuthenticationManager(
                "https://www.example.com/base/authentication/logout-callback?code=1234");

            var fetchDataUrl = "https://www.example.com/base/fetchData";
            remoteAuthenticator.AuthenticationState.ReturnUrl = fetchDataUrl;

            authServiceMock.Setup(s => s.CompleteSignOutAsync(It.IsAny<RemoteAuthenticationContext<RemoteAuthenticationState>>()))
                .ReturnsAsync(new RemoteAuthenticationResult<RemoteAuthenticationState>()
                {
                    Status = RemoteAuthenticationStatus.Failure,
                    ErrorMessage = "There was an error trying to log out"
                });

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [_action] = RemoteAuthenticationActions.LogOutCallback
            });

            // Act
            await renderer.Dispatcher.InvokeAsync<object>(() => remoteAuthenticator.SetParametersAsync(parameters));

            // Assert
            Assert.Equal(
                "https://www.example.com/base/authentication/logout-failed?message=There was an error trying to log out",
                remoteAuthenticator.Navigation.Uri);

            authServiceMock.Verify();
        }

        public static TheoryData<UIValidator> DisplaysRightUIData { get; } = new TheoryData<UIValidator>
        {
            { new UIValidator {
                Action = "login", SetupAction = (validator, remoteAuthenticator) => { remoteAuthenticator.LogingIn = validator.Render; } }
            },
            { new UIValidator {
                Action = "login-callback", SetupAction = (validator, remoteAuthenticator) => { remoteAuthenticator.CompletingLogingIn = validator.Render; } }
            },
            { new UIValidator {
                Action = "login-failed", SetupAction = (validator, remoteAuthenticator) => { remoteAuthenticator.LogInFailed = m => builder => validator.Render(builder); } }
            },
            { new UIValidator {
                Action = "profile", SetupAction = (validator, remoteAuthenticator) => { remoteAuthenticator.LogingIn = validator.Render; } }
            },
            // Profile fragment overrides
            { new UIValidator {
                Action = "profile", SetupAction = (validator, remoteAuthenticator) => { remoteAuthenticator.UserProfile = validator.Render; } }
            },
            { new UIValidator {
                Action = "register", SetupAction = (validator, remoteAuthenticator) => { remoteAuthenticator.LogingIn = validator.Render; } }
            },
            // Register fragment overrides
            { new UIValidator {
                Action = "register", SetupAction = (validator, remoteAuthenticator) => { remoteAuthenticator.Registering = validator.Render; } }
            },
            { new UIValidator {
                Action = "logout", SetupAction = (validator, remoteAuthenticator) => { remoteAuthenticator.LogOut = validator.Render; } }
            },
            { new UIValidator {
                Action = "logout-callback", SetupAction = (validator, remoteAuthenticator) => { remoteAuthenticator.CompletingLogOut = validator.Render; } }
            },
            { new UIValidator {
                Action = "logout-failed", SetupAction = (validator, remoteAuthenticator) => { remoteAuthenticator.LogOutFailed = m => builder => validator.Render(builder); } }
            },
            { new UIValidator {
                Action = "logged-out", SetupAction = (validator, remoteAuthenticator) => { remoteAuthenticator.LogOutSucceded = validator.Render; } }
            },
        };

        [Theory]
        [MemberData(nameof(DisplaysRightUIData))]
        public async Task AuthenticationManager_DisplaysRightUI_ForEachStateAsync(UIValidator validator)
        {
            // Arrange
            var renderer = new TestRenderer(new ServiceCollection().BuildServiceProvider());
            var authenticator = new TestRemoteAuthenticatorView();
            renderer.Attach(authenticator);
            validator.Setup(authenticator);

            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [_action] = validator.Action
            });

            // Act
            await renderer.Dispatcher.InvokeAsync<object>(() => authenticator.SetParametersAsync(parameters));

            // Assert
            Assert.True(validator.WasCalled);
        }

        public class UIValidator
        {
            public string Action { get; set; }
            public Action<UIValidator, RemoteAuthenticatorViewCore<RemoteAuthenticationState>> SetupAction { get; set; }
            public bool WasCalled { get; set; }
            public RenderFragment Render { get; set; }

            public UIValidator() => Render = builder => WasCalled = true;

            internal void Setup(TestRemoteAuthenticatorView manager) => SetupAction(this, manager);
        }

        private static
            (RemoteAuthenticatorViewCore<RemoteAuthenticationState> manager,
            TestRenderer renderer,
            Mock<IRemoteAuthenticationService<RemoteAuthenticationState>> authenticationServiceMock,
            TestJsRuntime js)

            CreateAuthenticationManager(
            string currentUri,
            string baseUri = "https://www.example.com/base/")
        {
            var renderer = new TestRenderer(new ServiceCollection().BuildServiceProvider());
            var remoteAuthenticator = new RemoteAuthenticatorViewCore<RemoteAuthenticationState>();
            renderer.Attach(remoteAuthenticator);

            remoteAuthenticator.Navigation = new TestNavigationManager(
                baseUri,
                currentUri);

            remoteAuthenticator.AuthenticationState = new RemoteAuthenticationState();
            remoteAuthenticator.ApplicationPaths = new RemoteAuthenticationApplicationPathsOptions();

            var authenticationServiceMock = new Mock<IRemoteAuthenticationService<RemoteAuthenticationState>>();

            remoteAuthenticator.AuthenticationService = authenticationServiceMock.Object;
            var jsRuntime = new TestJsRuntime();
            remoteAuthenticator.JS = jsRuntime;
            return (remoteAuthenticator, renderer, authenticationServiceMock, jsRuntime);
        }

        private class TestNavigationManager : NavigationManager
        {
            public TestNavigationManager(string baseUrl, string currentUrl) => Initialize(baseUrl, currentUrl);

            protected override void NavigateToCore(string uri, bool forceLoad) => Uri = uri;
        }

        private class TestJsRuntime : IJSRuntime
        {
            public (string identifier, object[] args) LastInvocation { get; set; }
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object[] args)
            {
                LastInvocation = (identifier, args);
                return default;
            }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object[] args)
            {
                LastInvocation = (identifier, args);
                return default;
            }
        }

        public class TestRemoteAuthenticatorView : RemoteAuthenticatorViewCore<RemoteAuthenticationState>
        {
            public TestRemoteAuthenticatorView()
            {
                ApplicationPaths = new RemoteAuthenticationApplicationPathsOptions() {
                    RemoteProfilePath = "Identity/Account/Manage",
                    RemoteRegisterPath = "Identity/Account/Register",
                };
            }

            protected override Task OnParametersSetAsync()
            {
                if (Action == "register" || Action == "profile")
                {
                    return base.OnParametersSetAsync();
                }

                return Task.CompletedTask;
            }
        }

        private class TestRenderer : Renderer
        {
            public TestRenderer(IServiceProvider services)
                : base(services, NullLoggerFactory.Instance)
            {
            }

            public int Attach(IComponent component) => AssignRootComponentId(component);

            private static readonly Dispatcher _dispatcher = Dispatcher.CreateDefault();

            public override Dispatcher Dispatcher => _dispatcher;

            protected override void HandleException(Exception exception)
                => ExceptionDispatchInfo.Capture(exception).Throw();

            protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) =>
                Task.CompletedTask;
        }
    }
}