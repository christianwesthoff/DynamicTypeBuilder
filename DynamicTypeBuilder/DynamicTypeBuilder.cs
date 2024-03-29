﻿#region License
/*
    Nequeo Pty Ltd License
    
    Permission is hereby granted, free of charge, to any person
    obtaining a copy of this software and associated documentation
    files (the "Software"), to deal in the Software without
    restriction, including without limitation the rights to use,
    copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the
    Software is furnished to do so, subject to the following
    conditions:

    The above copyright notice and this permission notice shall be
    included in all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
    EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
    OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
    NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
    HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
    WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
    FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
    OTHER DEALINGS IN THE SOFTWARE.
*/
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;

namespace DynamicTypeBuilder
{
    /// <summary>
    /// Creates a new type dynamically.
    /// </summary>
    public sealed class DynamicTypeBuilder : IDynamicTypeBuilder
    {
        private AssemblyName _assemblyName;
        private AssemblyBuilder _asssemblyBuilder;

        private ModuleBuilder _moduleBuilder;
        private Dictionary<SignatureBuilder, Type> _classes;

        private ReaderWriterLock _rwLock;
        private TypeBuilder _typeBuilder;
        private string _typeName;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="moduleName">The name of the assembly module.</param>
        public DynamicTypeBuilder(string moduleName)
        {
            // Make sure the page reference exists.
            if (moduleName == null) throw new ArgumentNullException(nameof(moduleName));

            // Create the nw assembly
            _assemblyName = new AssemblyName(moduleName);
            _asssemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(_assemblyName, AssemblyBuilderAccess.Run);

            // Create only one module, therefore the
            // module name is the assembly name.
            _moduleBuilder = _asssemblyBuilder.DefineDynamicModule(_assemblyName.Name);

            // Get the class unique signature.
            _classes = new Dictionary<SignatureBuilder, Type>();
            _rwLock = new ReaderWriterLock();
        }

        /// <summary>
        /// Create a new instance of the dynamic type.
        /// </summary>
        /// <param name="typeName">The name of the type.</param>
        /// <param name="properties">The collection of properties to create in the type.</param>
        /// <returns>The new instance of the type.</returns>
        public object Create(string typeName, IEnumerable<DynamicProperty> properties)
        {
            // Make sure the page reference exists.
            if (typeName == null) throw new ArgumentNullException(nameof(typeName));
            if (properties == null) throw new ArgumentNullException(nameof(properties));

            _typeName = typeName;

            // Return the create type.
            return CreateEx(typeName, properties, null);
        }

        /// <summary>
        /// Create a new instance of the dynamic type.
        /// </summary>
        /// <param name="typeName">The name of the type.</param>
        /// <param name="properties">The collection of properties to create in the type.</param>
        /// <param name="methods">The collection of methods to create in the type.</param>
        /// <returns>The new instance of the type.</returns>
        public object Create(string typeName, IEnumerable<DynamicProperty> properties, IEnumerable<DynamicMethod> methods)
        {
            // Make sure the page reference exists.
            if (typeName == null) throw new ArgumentNullException(nameof(typeName));
            if (properties == null) throw new ArgumentNullException(nameof(properties));
            if (methods == null) throw new ArgumentNullException(nameof(methods));

            _typeName = typeName;

            // Return the create type.
            return CreateEx(typeName, properties, methods);
        }

        /// <summary>
        /// Create a new instance of the dynamic type.
        /// </summary>
        /// <param name="typeName">The name of the type.</param>
        /// <param name="properties">The collection of properties to create in the type.</param>
        /// <returns>The new instance of the type.</returns>
        public object Create(string typeName, IEnumerable<DynamicPropertyValue> properties)
        {
            // Make sure the page reference exists.
            if (typeName == null) throw new ArgumentNullException(nameof(typeName));
            if (properties == null) throw new ArgumentNullException(nameof(properties));

            _typeName = typeName;

            // Create the dynamic type collection
            var prop = new List<DynamicProperty>();
            foreach (var item in properties)
                prop.Add(new DynamicProperty(item.Name, item.Type));

            // Return the create type.
            var instance = CreateEx(typeName, prop.ToArray(), null);
            var infos = instance.GetType().GetProperties();

            // Assign each type value
            foreach (var info in infos)
                info.SetValue(instance, properties.First(u => u.Name == info.Name).Value, null);

            // Return the instance with values assigned.
            return instance;
        }

        /// <summary>
        /// Create a new instance of the dynamic type.
        /// </summary>
        /// <param name="typeName">The name of the type.</param>
        /// <param name="properties">The collection of properties to create in the type.</param>
        /// <param name="methods">The collection of methods to create in the type.</param>
        /// <returns>The new instance of the type.</returns>
        public object Create(string typeName, IEnumerable<DynamicPropertyValue> properties, IEnumerable<DynamicMethod> methods)
        {
            // Make sure the page reference exists.
            if (typeName == null) throw new ArgumentNullException(nameof(typeName));
            if (properties == null) throw new ArgumentNullException(nameof(properties));
            if (methods == null) throw new ArgumentNullException(nameof(methods));

            _typeName = typeName;

            // Create the dynamic type collection
            var prop = new List<DynamicProperty>();
            foreach (var item in properties)
                prop.Add(new DynamicProperty(item.Name, item.Type));

            // Return the create type.
            var instance = CreateEx(typeName, prop.ToArray(), methods);
            var infos = instance.GetType().GetProperties();

            // Assign each type value
            foreach (var info in infos)
                info.SetValue(instance, properties.First(u => u.Name == info.Name).Value, null);

            // Return the instance with values assigned.
            return instance;
        }

        /// <summary>
        /// Create a new instance of the dynamic type.
        /// </summary>
        /// <param name="typeName">The name of the type.</param>
        /// <param name="properties">The collection of properties to create in the type.</param>
        /// <param name="methods">The collection of methods to create in the type.</param>
        /// <returns>The new instance of the type.</returns>
        private object CreateEx(string typeName, IEnumerable<DynamicProperty> properties, IEnumerable<DynamicMethod> methods)
        {
            // Create the dynamic class.
            var type = GetDynamicClass(properties, methods);

            // Return the new instance of the type.
            return Activator.CreateInstance(type);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="methods"></param>
        /// <returns></returns>
        private Type GetDynamicClass(IEnumerable<DynamicProperty> properties, IEnumerable<DynamicMethod> methods)
        {
            _rwLock.AcquireReaderLock(Timeout.Infinite);
            try
            {
                var signature = new SignatureBuilder(properties, methods);
                Type type;
                if (!_classes.TryGetValue(signature, out type))
                {
                    type = CreateDynamicClass(signature.properties, signature.methods);
                    _classes.Add(signature, type);
                }
                return type;
            }
            finally
            {
                _rwLock.ReleaseReaderLock();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="methods"></param>
        /// <returns></returns>
        private Type CreateDynamicClass(DynamicProperty[] properties, DynamicMethod[] methods)
        {
            var cookie = _rwLock.UpgradeToWriterLock(Timeout.Infinite);
            try
            {
                var typeName = _typeName ;

                try
                {
                    _typeBuilder = _moduleBuilder.DefineType(typeName, TypeAttributes.Class |
                        TypeAttributes.Public, typeof(DynamicClass));

                    CreateConstructor(_typeBuilder);
                    var fields = GenerateProperties(_typeBuilder, properties);
                    GenerateEquals(_typeBuilder, fields);
                    GenerateGetHashCode(_typeBuilder, fields);

                    if (methods != null)
                        GenerateMethods(_typeBuilder, methods);

                    // Create the type, return the type.
                    var result = _typeBuilder.CreateType();
                    return result;
                }
                finally { }
            }
            finally
            {
                _rwLock.DowngradeFromWriterLock(ref cookie);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="typeBuilder"></param>
        private void CreateConstructor(TypeBuilder typeBuilder)
        {
            // Create the default constructor.
            var baseConstructorInfo = typeof(object).GetConstructor(new Type[0]);
            var constructorBuilder = 
                typeBuilder.DefineConstructor(
                           MethodAttributes.Public,
                           CallingConventions.Standard,
                           Type.EmptyTypes);

            // Create the base call operations.
            var ilGenerator = constructorBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Call, baseConstructorInfo);
            ilGenerator.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tb"></param>
        /// <param name="methods"></param>
        private void GenerateMethods(TypeBuilder tb, DynamicMethod[] methods)
        {
            for (var i = 0; i < methods.Length; i++)
            {
                var dm = methods[i];
                MethodBuilder mb;
                ILGenerator mdMethod;

                // If a build action exists.
                if (dm.BuildAction != null)
                {
                    // Execute the custom build action.
                    dm.BuildAction(tb);
                }
                else if (dm.ReturnType != typeof(void) && dm.Parameters != null)
                {
                    mb = tb.DefineMethod(dm.Name, MethodAttributes.Public |
                        MethodAttributes.SpecialName | MethodAttributes.HideBySig, dm.ReturnType, dm.Parameters.ToArray());
                    mdMethod = mb.GetILGenerator();

                    // Load the instance it belongs to (argument zero is the instance)
                    var localBuilder = mdMethod.DeclareLocal(dm.ReturnType);
                    mdMethod.Emit(OpCodes.Ldloc, localBuilder); 
                    mdMethod.Emit(OpCodes.Ret);
                }
                else if (dm.ReturnType != typeof(void) && dm.Parameters == null)
                {
                    mb = tb.DefineMethod(dm.Name, MethodAttributes.Public |
                        MethodAttributes.SpecialName | MethodAttributes.HideBySig, dm.ReturnType, null);
                    mdMethod = mb.GetILGenerator();

                    // Load the instance it belongs to (argument zero is the instance)
                    var localBuilder = mdMethod.DeclareLocal(dm.ReturnType);
                    mdMethod.Emit(OpCodes.Ldloc, localBuilder); 
                    mdMethod.Emit(OpCodes.Ret);
                }
                else if (dm.ReturnType == typeof(void) && dm.Parameters != null)
                {
                    mb = tb.DefineMethod(dm.Name, MethodAttributes.Public |
                        MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(void), dm.Parameters.ToArray());
                    mdMethod = mb.GetILGenerator();

                    // Load the instance it belongs to (argument zero is the instance)
                    mdMethod.Emit(OpCodes.Ret);
                }
                else
                {
                    mb = tb.DefineMethod(dm.Name, MethodAttributes.Public |
                        MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(void), null);
                    mdMethod = mb.GetILGenerator();

                    // Load the instance it belongs to (argument zero is the instance)
                    mdMethod.Emit(OpCodes.Ret);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tb"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        private FieldInfo[] GenerateProperties(TypeBuilder tb, DynamicProperty[] properties)
        {
            FieldInfo[] fields = new FieldBuilder[properties.Length];
            for (var i = 0; i < properties.Length; i++)
            {
                var dp = properties[i];
                var fb = tb.DefineField("_" + dp.Name, dp.Type, FieldAttributes.Private);
                var pb = tb.DefineProperty(dp.Name, PropertyAttributes.HasDefault, dp.Type, null);
                var mbGet = tb.DefineMethod("get_" + dp.Name,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    dp.Type, Type.EmptyTypes);
                var genGet = mbGet.GetILGenerator();
                genGet.Emit(OpCodes.Ldarg_0);
                genGet.Emit(OpCodes.Ldfld, fb);
                genGet.Emit(OpCodes.Ret);
                var mbSet = tb.DefineMethod("set_" + dp.Name,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    null, new Type[] { dp.Type });
                var genSet = mbSet.GetILGenerator();
                genSet.Emit(OpCodes.Ldarg_0);
                genSet.Emit(OpCodes.Ldarg_1);
                genSet.Emit(OpCodes.Stfld, fb);
                genSet.Emit(OpCodes.Ret);
                pb.SetGetMethod(mbGet);
                pb.SetSetMethod(mbSet);
                fields[i] = fb;
            }
            return fields;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tb"></param>
        /// <param name="fields"></param>
        private void GenerateEquals(TypeBuilder tb, FieldInfo[] fields)
        {
            var mb = tb.DefineMethod("Equals",
                MethodAttributes.Public | MethodAttributes.ReuseSlot |
                MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(bool), new Type[] { typeof(object) });
            var gen = mb.GetILGenerator();
            var other = gen.DeclareLocal(tb);
            var next = gen.DefineLabel();
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Isinst, tb);
            gen.Emit(OpCodes.Stloc, other);
            gen.Emit(OpCodes.Ldloc, other);
            gen.Emit(OpCodes.Brtrue_S, next);
            gen.Emit(OpCodes.Ldc_I4_0);
            gen.Emit(OpCodes.Ret);
            gen.MarkLabel(next);
            foreach (var field in fields)
            {
                var ft = field.FieldType;
                var ct = typeof(EqualityComparer<>).MakeGenericType(ft);
                next = gen.DefineLabel();
                gen.EmitCall(OpCodes.Call, ct.GetMethod("get_Default"), null);
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, field);
                gen.Emit(OpCodes.Ldloc, other);
                gen.Emit(OpCodes.Ldfld, field);
                gen.EmitCall(OpCodes.Callvirt, ct.GetMethod("Equals", new Type[] { ft, ft }), null);
                gen.Emit(OpCodes.Brtrue_S, next);
                gen.Emit(OpCodes.Ldc_I4_0);
                gen.Emit(OpCodes.Ret);
                gen.MarkLabel(next);
            }
            gen.Emit(OpCodes.Ldc_I4_1);
            gen.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tb"></param>
        /// <param name="fields"></param>
        private void GenerateGetHashCode(TypeBuilder tb, FieldInfo[] fields)
        {
            var mb = tb.DefineMethod("GetHashCode",
                MethodAttributes.Public | MethodAttributes.ReuseSlot |
                MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(int), Type.EmptyTypes);
            var gen = mb.GetILGenerator();
            gen.Emit(OpCodes.Ldc_I4_0);
            foreach (var field in fields)
            {
                var ft = field.FieldType;
                var ct = typeof(EqualityComparer<>).MakeGenericType(ft);
                gen.EmitCall(OpCodes.Call, ct.GetMethod("get_Default"), null);
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, field);
                gen.EmitCall(OpCodes.Callvirt, ct.GetMethod("GetHashCode", new Type[] { ft }), null);
                gen.Emit(OpCodes.Xor);
            }
            gen.Emit(OpCodes.Ret);
        }
    }

    /// <summary>
    /// Dynamic class descriptor
    /// </summary>
    public abstract class DynamicClass
    {
        /// <summary>
        /// The to string override.
        /// </summary>
        /// <returns>The string translation.</returns>
        public override string ToString()
        {
            var props = this.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            var sb = new StringBuilder();
            sb.Append("{");
            for (var i = 0; i < props.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(props[i].Name);
                sb.Append("=");
                sb.Append(props[i].GetValue(this, null));
            }
            sb.Append("}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Dynamic method builder.
    /// </summary>
    public class DynamicMethod
    {
        string name;
        IEnumerable<Type> parameters;
        Type returnType;
        Action<TypeBuilder> buildAction = null;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="name">The name of the method.</param>
        /// <param name="parameters">The collection parameter types.</param>
        /// <param name="returnType">The return type.</param>
        public DynamicMethod(string name, IEnumerable<Type> parameters, Type returnType)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            this.name = name;
            this.parameters = parameters;
            this.returnType = returnType;
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="name">The name of the method.</param>
        /// <param name="parameters">The collection parameter types.</param>
        /// <param name="returnType">The return type.</param>
        /// <param name="buildAction">The build action.</param>
        public DynamicMethod(string name, IEnumerable<Type> parameters, Type returnType, Action<TypeBuilder> buildAction)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            this.name = name;
            this.parameters = parameters;
            this.returnType = returnType;
            this.buildAction = buildAction;
        }

        /// <summary>
        /// Gets, the method name.
        /// </summary>
        public string Name => name;

        /// <summary>
        /// Gets, the collection of parameters
        /// </summary>
        public IEnumerable<Type> Parameters => parameters;

        /// <summary>
        /// Gets, the return type.
        /// </summary>
        public Type ReturnType => returnType;

        /// <summary>
        /// Gets, build action.
        /// </summary>
        public Action<TypeBuilder> BuildAction => buildAction;
    }

    /// <summary>
    /// Dynamic property builder.
    /// </summary>
    public class DynamicProperty
    {
        string name;
        Type type;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="type">The type of the property.</param>
        public DynamicProperty(string name, Type type)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (type == null) throw new ArgumentNullException(nameof(type));
            this.name = name;
            this.type = type;
        }

        /// <summary>
        /// Gets, the property name.
        /// </summary>
        public string Name => name;

        /// <summary>
        /// Gets, the property type.
        /// </summary>
        public Type Type => type;
    }

    /// <summary>
    /// Dynamic property builder, with value assigned.
    /// </summary>
    public class DynamicPropertyValue
    {
        object value;
        string name;
        Type type;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="type">The type of the property</param>
        /// <param name="value">The value of the property.</param>
        public DynamicPropertyValue(string name, Type type, object value)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (value == null) throw new ArgumentNullException(nameof(value));
            this.name = name;
            this.type = type;
            this.value = value;
        }

        /// <summary>
        /// Gets, the property name.
        /// </summary>
        public string Name => name;

        /// <summary>
        /// Gets, the property type.
        /// </summary>
        public Type Type => type;

        /// <summary>
        /// Gets, the property value.
        /// </summary>
        public object Value => value;
    }

    /// <summary>
    /// Dynamic expression class builder.
    /// </summary>
    public class DynamicClassBuilder
    {
        /// <summary>
        /// The static instance of the type.
        /// </summary>
        public static readonly DynamicClassBuilder Instance = new DynamicClassBuilder();

        /// <summary>
        /// 
        /// </summary>
        static DynamicClassBuilder() { }  // Trigger lazy initialization of static fields

        ModuleBuilder module;
        Dictionary<Signature, Type> classes;
        int classCount;
        ReaderWriterLock rwLock;

        /// <summary>
        /// 
        /// </summary>
        private DynamicClassBuilder()
        {
            var name = new AssemblyName("DynamicClasses");
            var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
            //#if ENABLE_LINQ_PARTIAL_TRUST
            //new ReflectionPermission(PermissionState.Unrestricted).Assert();
            //#endif
            try
            {
                module = assembly.DefineDynamicModule("Module");
            }
            finally
            {
                //#if ENABLE_LINQ_PARTIAL_TRUST
                //PermissionSet.RevertAssert();
                //#endif
            }
            classes = new Dictionary<Signature, Type>();
            rwLock = new ReaderWriterLock();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        public Type GetDynamicClass(IEnumerable<DynamicProperty> properties)
        {
            rwLock.AcquireReaderLock(Timeout.Infinite);
            try
            {
                var signature = new Signature(properties);
                Type type;
                if (!classes.TryGetValue(signature, out type))
                {
                    type = CreateDynamicClass(signature.properties);
                    classes.Add(signature, type);
                }
                return type;
            }
            finally
            {
                rwLock.ReleaseReaderLock();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        Type CreateDynamicClass(DynamicProperty[] properties)
        {
            var cookie = rwLock.UpgradeToWriterLock(Timeout.Infinite);
            try
            {
                var typeName = "DynamicClass" + (classCount + 1);
                //#if ENABLE_LINQ_PARTIAL_TRUST
                //new ReflectionPermission(PermissionState.Unrestricted).Assert();
                //#endif
                try
                {
                    var tb = this.module.DefineType(typeName, TypeAttributes.Class |
                        TypeAttributes.Public, typeof(DynamicClass));
                    var fields = GenerateProperties(tb, properties);
                    GenerateEquals(tb, fields);
                    GenerateGetHashCode(tb, fields);
                    var result = tb.CreateType();
                    classCount++;
                    return result;
                }
                finally
                {
                    //#if ENABLE_LINQ_PARTIAL_TRUST
                    //PermissionSet.RevertAssert();
                    //#endif
                }
            }
            finally
            {
                rwLock.DowngradeFromWriterLock(ref cookie);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tb"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        FieldInfo[] GenerateProperties(TypeBuilder tb, DynamicProperty[] properties)
        {
            FieldInfo[] fields = new FieldBuilder[properties.Length];
            for (var i = 0; i < properties.Length; i++)
            {
                var dp = properties[i];
                var fb = tb.DefineField("_" + dp.Name, dp.Type, FieldAttributes.Private);
                var pb = tb.DefineProperty(dp.Name, PropertyAttributes.HasDefault, dp.Type, null);
                var mbGet = tb.DefineMethod("get_" + dp.Name,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    dp.Type, Type.EmptyTypes);
                var genGet = mbGet.GetILGenerator();
                genGet.Emit(OpCodes.Ldarg_0);
                genGet.Emit(OpCodes.Ldfld, fb);
                genGet.Emit(OpCodes.Ret);
                var mbSet = tb.DefineMethod("set_" + dp.Name,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    null, new Type[] { dp.Type });
                var genSet = mbSet.GetILGenerator();
                genSet.Emit(OpCodes.Ldarg_0);
                genSet.Emit(OpCodes.Ldarg_1);
                genSet.Emit(OpCodes.Stfld, fb);
                genSet.Emit(OpCodes.Ret);
                pb.SetGetMethod(mbGet);
                pb.SetSetMethod(mbSet);
                fields[i] = fb;
            }
            return fields;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tb"></param>
        /// <param name="fields"></param>
        void GenerateEquals(TypeBuilder tb, FieldInfo[] fields)
        {
            var mb = tb.DefineMethod("Equals",
                MethodAttributes.Public | MethodAttributes.ReuseSlot |
                MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(bool), new Type[] { typeof(object) });
            var gen = mb.GetILGenerator();
            var other = gen.DeclareLocal(tb);
            var next = gen.DefineLabel();
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Isinst, tb);
            gen.Emit(OpCodes.Stloc, other);
            gen.Emit(OpCodes.Ldloc, other);
            gen.Emit(OpCodes.Brtrue_S, next);
            gen.Emit(OpCodes.Ldc_I4_0);
            gen.Emit(OpCodes.Ret);
            gen.MarkLabel(next);
            foreach (var field in fields)
            {
                var ft = field.FieldType;
                var ct = typeof(EqualityComparer<>).MakeGenericType(ft);
                next = gen.DefineLabel();
                gen.EmitCall(OpCodes.Call, ct.GetMethod("get_Default"), null);
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, field);
                gen.Emit(OpCodes.Ldloc, other);
                gen.Emit(OpCodes.Ldfld, field);
                gen.EmitCall(OpCodes.Callvirt, ct.GetMethod("Equals", new Type[] { ft, ft }), null);
                gen.Emit(OpCodes.Brtrue_S, next);
                gen.Emit(OpCodes.Ldc_I4_0);
                gen.Emit(OpCodes.Ret);
                gen.MarkLabel(next);
            }
            gen.Emit(OpCodes.Ldc_I4_1);
            gen.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tb"></param>
        /// <param name="fields"></param>
        void GenerateGetHashCode(TypeBuilder tb, FieldInfo[] fields)
        {
            var mb = tb.DefineMethod("GetHashCode",
                MethodAttributes.Public | MethodAttributes.ReuseSlot |
                MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(int), Type.EmptyTypes);
            var gen = mb.GetILGenerator();
            gen.Emit(OpCodes.Ldc_I4_0);
            foreach (var field in fields)
            {
                var ft = field.FieldType;
                var ct = typeof(EqualityComparer<>).MakeGenericType(ft);
                gen.EmitCall(OpCodes.Call, ct.GetMethod("get_Default"), null);
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, field);
                gen.EmitCall(OpCodes.Callvirt, ct.GetMethod("GetHashCode", new Type[] { ft }), null);
                gen.Emit(OpCodes.Xor);
            }
            gen.Emit(OpCodes.Ret);
        }
    }

    /// <summary>
    /// Dynamic class signature.
    /// </summary>
    internal class SignatureBuilder : IEquatable<SignatureBuilder>
    {
        public DynamicMethod[] methods;
        public DynamicProperty[] properties;
        public int hashCode;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="methods"></param>
        public SignatureBuilder(IEnumerable<DynamicProperty> properties, IEnumerable<DynamicMethod> methods)
        {
            this.properties = properties.ToArray();

            if(methods != null)
                this.methods = methods.ToArray();

            hashCode = 0;
            foreach (var p in properties)
            {
                hashCode ^= p.Name.GetHashCode() ^ p.Type.GetHashCode();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return hashCode;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            return obj is SignatureBuilder ? Equals((SignatureBuilder)obj) : false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(SignatureBuilder other)
        {
            if (properties.Length != other.properties.Length) return false;
            for (var i = 0; i < properties.Length; i++)
            {
                if (properties[i].Name != other.properties[i].Name ||
                    properties[i].Type != other.properties[i].Type) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Dynamic class signature.
    /// </summary>
    internal class Signature : IEquatable<Signature>
    {
        public DynamicProperty[] properties;
        public int hashCode;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        public Signature(IEnumerable<DynamicProperty> properties)
        {
            this.properties = properties.ToArray();
            hashCode = 0;
            foreach (var p in properties)
            {
                hashCode ^= p.Name.GetHashCode() ^ p.Type.GetHashCode();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return hashCode;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            return obj is Signature ? Equals((Signature)obj) : false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(Signature other)
        {
            if (properties.Length != other.properties.Length) return false;
            for (var i = 0; i < properties.Length; i++)
            {
                if (properties[i].Name != other.properties[i].Name ||
                    properties[i].Type != other.properties[i].Type) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Dynamic type builder interface.
    /// </summary>
    public interface IDynamicTypeBuilder
    {
        /// <summary>
        /// Create a new instance of the dynamic type.
        /// </summary>
        /// <param name="typeName">The name of the type.</param>
        /// <param name="properties">The collection of properties to create in the type.</param>
        /// <returns>The new instance of the type.</returns>
        object Create(string typeName, IEnumerable<DynamicProperty> properties);

        /// <summary>
        /// Create a new instance of the dynamic type.
        /// </summary>
        /// <param name="typeName">The name of the type.</param>
        /// <param name="properties">The collection of properties to create in the type.</param>
        /// <param name="methods">The collection of methods to create in the type.</param>
        /// <returns>The new instance of the type.</returns>
        object Create(string typeName, IEnumerable<DynamicProperty> properties, IEnumerable<DynamicMethod> methods);

        /// <summary>
        /// Create a new instance of the dynamic type.
        /// </summary>
        /// <param name="typeName">The name of the type.</param>
        /// <param name="properties">The collection of properties to create in the type.</param>
        /// <returns>The new instance of the type.</returns>
        object Create(string typeName, IEnumerable<DynamicPropertyValue> properties);

        /// <summary>
        /// Create a new instance of the dynamic type.
        /// </summary>
        /// <param name="typeName">The name of the type.</param>
        /// <param name="properties">The collection of properties to create in the type.</param>
        /// <param name="methods">The collection of methods to create in the type.</param>
        /// <returns>The new instance of the type.</returns>
        object Create(string typeName, IEnumerable<DynamicPropertyValue> properties, IEnumerable<DynamicMethod> methods);
    }
}