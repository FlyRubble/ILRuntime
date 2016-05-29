﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;

using ILRuntime.CLR.TypeSystem;
using ILRuntime.CLR.Method;
using ILRuntime.CLR.Utils;
using ILRuntime.Runtime.Intepreter;
using ILRuntime.Runtime.Stack;
namespace ILRuntime.Runtime.Enviorment
{
    public class AppDomain
    {
        HashSet<string> loadedAssembly;
        Queue<ILIntepreter> freeIntepreters = new Queue<ILIntepreter>();
        Dictionary<string, IType> mapType = new Dictionary<string, IType>();
        Dictionary<int, IMethod> mapMethod = new Dictionary<int, IMethod>();
        IType voidType;
        public AppDomain()
        {
        }

        public IType VoidType { get { return voidType; } }
        public void LoadAssembly(System.IO.Stream stream)
        {
            LoadAssembly(stream, null, null);
        }
        public void LoadAssembly(System.IO.Stream stream, System.IO.Stream symbol, ISymbolReaderProvider symbolReader)
        {
            var module = ModuleDefinition.ReadModule(stream);

            if (symbolReader != null && symbol != null)
            {
                module.ReadSymbols(symbolReader.GetSymbolReader(module, symbol));
            }
            if (module.HasAssemblyReferences)
            {
                foreach (var ar in module.AssemblyReferences)
                {
                    /*if (moduleref.Contains(ar.Name) == false)
                        moduleref.Add(ar.Name);
                    if (moduleref.Contains(ar.FullName) == false)
                        moduleref.Add(ar.FullName);*/
                }
            }
            if (module.HasTypes)
            {
                List<ILType> types = new List<ILType>();
                foreach (var t in module.Types)
                {
                    ILType type = new ILType(t);
                    mapType[t.FullName] = type;
                    types.Add(type);
                }

                foreach (var t in types)
                {
                    t.InitializeBaseType(this);
                }
            }

            voidType = GetType("System.Void");
        }
        
        public IType GetType(string fullname)
        {
            IType res;
            if (mapType.TryGetValue(fullname, out res))
                return res;
            Type t = Type.GetType(fullname);
            if(t != null)
            {
                res = new CLRType(t, this);
                mapType[fullname] = res;
                ((CLRType)res).Initialize();
                return res;               
            }
            return null;
        }

        public object Invoke(string type, string method, params object[] p)
        {
            IType t = GetType(type);
            var m = t.GetMethod(method, p.Length);

            if(m != null)
            {
                return Invoke(m, p);
            }
            return null;
        }

        public object Invoke(IMethod m, params object[] p)
        {
            if (m is ILMethod)
            {
                ILIntepreter inteptreter = null;
                lock (freeIntepreters)
                {
                    if (freeIntepreters.Count > 0)
                        inteptreter = freeIntepreters.Dequeue();
                    else
                        inteptreter = new ILIntepreter(this);
                }
                inteptreter.Run((ILMethod)m, p);
            }
            return null;
        }

        public IMethod GetMethod(object token, ILType contextType)
        {
            string methodname = null;
            string typename = null;
            List<IType> paramList = null;
            int hashCode = token.GetHashCode();
            IMethod method;
            if(mapMethod.TryGetValue(hashCode, out method))
                return method;
            if (token is Mono.Cecil.MethodReference)
            {
                Mono.Cecil.MethodReference _ref = (token as Mono.Cecil.MethodReference);
                methodname = _ref.Name;
                typename = _ref.DeclaringType.FullName;

                paramList = _ref.GetParamList(this);
                if (_ref.IsGenericInstance)
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                throw new NotImplementedException();
                //Mono.Cecil.GenericInstanceMethod gmethod = _def as Mono.Cecil.GenericInstanceMethod;
                //genlist = new MethodParamList(environment, gmethod);
            }

            var type = GetType(typename);
            if (type == null)
                throw new KeyNotFoundException("Cannot find type:" + typename);

            method = type.GetMethod(methodname, paramList);

            mapMethod[hashCode] = method;
            return method;
        }

        public IMethod GetMethod(int tokenHash)
        {
            IMethod res;
            if (mapMethod.TryGetValue(tokenHash, out res))
                return res;

            return null;
        }
    }
}
