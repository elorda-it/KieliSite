using System;

namespace KieliWeb.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class NoRoleAttribute : Attribute
{
}
