﻿using System.Xml.Linq;
using AtlusScriptLibrary.Common.Libraries;

namespace AtlusScriptDocGen.Generators
{
    public class NppLibraryDocumentationGenerator : LibraryDocumentationGenerator
    {
        public NppLibraryDocumentationGenerator( Library library ) : base( library, DocumentationFormat.Npp )
        {
        }

        public override void Generate( string path )
        {
            var doc = new XDocument( 
                new XDeclaration( "1.0", "Windows-1252", "yes" ),
                new XComment( $"Generated by {Program.FullName}" ), 
                new XElement( "NotepadPlus" ) );

            var autoComplete = GenerateLanguageAutoComplete();
            doc.Root.Add( autoComplete );

            doc.Save( path );
        }

        private XElement GenerateLanguageAutoComplete()
        {
            var root = new XElement( "AutoComplete", new XAttribute( "language", "FlowScript" ) );

            // Define language environment parameters
            root.Add( new XElement( "Environment",
                                    new XAttribute( "ignoreCase", "no" ), new XAttribute( "startFunc", "(" ), new XAttribute( "stopFunc", ")" ),
                                    new XAttribute( "paramSeperator", "," ), new XAttribute( "terminal", ";" ), new XAttribute( "additionalWordChar", "." ) ) );

            // Each type, function, etc is defined as a 'KeyWord'
            foreach ( var module in Library.FlowScriptModules )
            {
                root.Add( new XComment( $"Generated from FlowScript Module '{module.Name}'" ) );

                if ( module.Functions != null && module.Functions.Count > 0 )
                {
                    root.Add( new XComment( "Functions" ) );
                    foreach ( var function in module.Functions )
                        root.Add( GenerateFunctionAutoComplete( function ) );
                }

                if ( module.Enums != null && module.Enums.Count > 0 )
                {
                    root.Add( new XComment( "Enums" ) );
                    foreach ( var @enum in module.Enums )
                    {
                        root.Add( GenerateSimpleDefinitionWithDescription( @enum.Name, @enum.Description ) );
                        foreach ( var member in @enum.Members )
                            root.Add( GenerateSimpleDefinitionWithDescription( $"{@enum.Name}.{member.Name}",
                                                                               $"{member.Value}. {member.Description}" ) );
                    }

                }

                if ( module.Constants != null && module.Constants.Count > 0 )
                {
                    root.Add( new XComment( "Constants" ) );
                    foreach ( var constant in module.Constants )
                        root.Add( GenerateSimpleDefinitionWithDescription( constant.Name,
                                                                           $"{constant.Type} {constant.Name} = {constant.Value}; {constant.Description}" ) );
                }
            }

            return root;
        }

        private static XElement GenerateFunctionAutoComplete( FlowScriptModuleFunction function )
        {
            var root = new XElement( "KeyWord", new XAttribute( "name", function.Name ), new XAttribute( "func", "yes" ) );
            var overload = new XElement( "Overload", new XAttribute( "retVal", function.ReturnType ), new XAttribute( "descr", function.Description ) );
            root.Add( overload );

            foreach ( var parameter in function.Parameters )
                overload.Add( new XElement( "Param", new XAttribute( "name", $"{parameter.Type} {parameter.Name}" ) ) );

            return root;
        }

        private static XElement GenerateSimpleDefinitionWithDescription( string name, string description )
        {
            return new XElement( "KeyWord", new XAttribute( "name", name ), new XAttribute( "func", "yes" ),
                                 new XElement( "Overload", new XAttribute( "retVal", "" ), new XAttribute( "descr", description ) ) );
        }
    }
}