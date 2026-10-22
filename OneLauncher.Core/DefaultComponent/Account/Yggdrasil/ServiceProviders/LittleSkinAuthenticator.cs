using System;
using System.Collections.Generic;
using System.Text;

namespace OneLauncher.Core.DefaultComponent.Account.Yggdrasil.ServiceProviders;
public class LittleSkinAuthenticator : YggdrasilAuthenticator
{
    public LittleSkinAuthenticator() : base()
    {
        
    }
    protected override string AuthApiRoot { get; } = "https://littleskin.cn/api/yggdrasil";
}
