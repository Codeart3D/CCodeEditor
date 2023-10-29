using CCodeEditorLib.Source;
using Codeart3D_Editor.Model;
using Codeart3D_Editor.Source;
using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CCodeEditorLib.CSharp
{
    public static class CSharpParser
    {
        private static string CodeHeader;
        private static List<Class> Classes = new List<Class>();

        private static List<Variable> Variables = new List<Variable>();
        private static List<Function> Functions = new List<Function>();
        private static List<Condition> Conditions = new List<Condition>();
        private static List<Expertion> Expertions = new List<Expertion>();
        private static List<Command> Commands { get; } = new List<Command>();

        public static string CheckErrors(string body)
        {
            string code = CodeHeader + body + " }}}";

            CSharpCodeProvider provider = new CSharpCodeProvider();
            ICodeCompiler compiler = provider.CreateCompiler();
            CompilerParameters compilerparams = new CompilerParameters();
            compilerparams.GenerateExecutable = false;
            compilerparams.GenerateInMemory = true;
            CompilerResults results = compiler.CompileAssemblyFromSource(compilerparams, code);

            if (results.Errors.HasErrors)
            {
                StringBuilder errors = new StringBuilder("Compiler Errors :\r\n");

                foreach (CompilerError error in results.Errors)
                {
                    errors.AppendFormat("Line {0},{1}\t: {2}\n", error.Line, error.Column, error.ErrorText);
                }

                return errors.ToString();
            }

            return null;
        }

        public static string Compile(string code)
        {
            string error = CheckErrors(code);

            if (error != null)
                return error;

            Commands.Clear();

            return StartCompile(code);
        }

        private static string StartCompile(string code)
        {
            int idx = 0;
            string[] expersions = code.Replace("\n", String.Empty).Split(';');

            foreach (var expe in expersions)
            {
                string exp = expe.Trim();

                if (!string.IsNullOrEmpty(exp))
                {
                    string[] v = exp.Split('=');

                    // check variable
                    if (exp.StartsWith("var "))
                    {
                        string[] n = v[0].Split(' ');
                        var vr = new Variable(n[1], v[1].Trim());
                        Variables.Add(vr);
                        Commands.Add(vr);
                    }
                    // check condition
                    else if (exp.StartsWith("if (") || exp.StartsWith("if("))
                    {
                        Condition cnd = new Condition(ConditionType.If, TextUtils.GetStringBetweenParanteses(exp, out idx));
                        Conditions.Add(cnd);
                        Commands.Add(cnd);
                        StartCompile(exp.Substring(idx, exp.Length - idx));
                    }
                    else if (exp.StartsWith("else if (") || exp.StartsWith("else if("))
                    {
                        Condition cnd = new Condition(ConditionType.ElseIf, TextUtils.GetStringBetweenParanteses(exp, out idx));
                        Conditions.Add(cnd);
                        Commands.Add(cnd);
                        StartCompile(exp.Substring(idx, exp.Length - idx));
                    }
                    else if (exp.StartsWith("else (") || exp.StartsWith("else("))
                    {
                        Condition cnd = new Condition(ConditionType.Else, TextUtils.GetStringBetweenParanteses(exp, out idx));
                        Conditions.Add(cnd);
                        Commands.Add(cnd);
                        StartCompile(exp.Substring(idx, exp.Length - idx));
                    }
                    else if (exp.EndsWith("++"))
                    {
                        Expertion expertion = new Expertion(ExpertionType.PlusPlus, exp.Substring(0, exp.Length - 2));
                        Expertions.Add(expertion);
                        Commands.Add(expertion);
                    }
                    else if (exp.StartsWith("return "))
                    {
                        Expertion expertion = new Expertion(ExpertionType.Return, exp.Substring(6, exp.Length - 6));
                        Expertions.Add(expertion);
                        Commands.Add(expertion);
                    }
                    else
                    {
                        // check function
                        foreach (var item in Classes)
                        {
                            Function find;

                            if (v.Length == 1)
                                find = item.Functions.Where(p => v[0].StartsWith(item.Name + "." + p.Name + "(")).FirstOrDefault();
                            else
                                find = item.Functions.Where(p => v[1].StartsWith(item.Name + "." + p.Name + "(")).FirstOrDefault();

                            if (find != null)
                            {
                                Function func = new Function(find.Name, TextUtils.GetStringBetweenParanteses(exp, out idx));
                                Functions.Add(func);
                                Commands.Add(func);
                                break;
                            }
                        }

                        // find set value after check functions
                        if (v.Length > 1)
                        {
                            Expertion expertion = new Expertion(ExpertionType.SetValue, v[0], v[1]);
                            Expertions.Add(expertion);
                            Commands.Add(expertion);
                        }
                    }
                }
            }

            return null;
        }

        public static void SetClasses(List<FunctionProperty> funcs, List<Asset> Objects)
        {
            StringBuilder builder = new StringBuilder(@"namespace Codeart { ");

            if (Debugger.IsAttached)
            {
                Class game = new Class("Game");
                game.Functions.Add(new Function("Start", null));
                game.Functions.Add(new Function("Stop", null));
                Classes.Add(game);
            }

            GenerateClasses(builder, funcs);
            builder.Append(@"public static class Function { ");
            GenerateObjects(builder, Objects);
            builder.Append(@" public static object Action() { ");
            CodeHeader = builder.ToString();
        }

        private static void GenerateClasses(StringBuilder builder, List<FunctionProperty> funcs)
        {
            if (funcs == null)
            {
                if (Debugger.IsAttached)
                {
                    foreach (var item in Classes)
                    {
                        builder.Append(@"public static class " + item.Name + " { ");

                        foreach (var fun in item.Functions)
                        {
                            builder.Append(@"public static void " + fun.Name + @"(){} ");
                        }

                        builder.Append("} ");
                    }
                }
            }
            else
            {
                ResourceType preclass = ResourceType.None;

                foreach (var item in funcs)
                {
                    if (preclass != item.Type)
                    {
                        if (preclass != ResourceType.None)
                            builder.Append("} ");

                        builder.Append(@"public class " + item.Type + " { ");
                    }

                        builder.Append(@"public void " + item.Name + @"(){} ");
                }

                builder.Append("} ");
            }
        }

        private static void GenerateObjects(StringBuilder builder, List<Asset> Objects)
        {
            if (Objects != null)
            {
                foreach (var item in Objects)
                    builder.Append($"public {item.AssetType} {item.Name} = new {item.AssetType}();");
            }
        }
    }
}
