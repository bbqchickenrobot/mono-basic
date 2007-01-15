' 
' Visual Basic.Net Compiler
' Copyright (C) 2004 - 2007 Rolf Bjarne Kvinge, RKvinge@novell.com
' 
' This library is free software; you can redistribute it and/or
' modify it under the terms of the GNU Lesser General Public
' License as published by the Free Software Foundation; either
' version 2.1 of the License, or (at your option) any later version.
' 
' This library is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY; without even the implied warranty of
' MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
' Lesser General Public License for more details.
' 
' You should have received a copy of the GNU Lesser General Public
' License along with this library; if not, write to the Free Software
' Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
' 

Imports System.Reflection
Imports System.Reflection.Emit

''' <summary>
''' Every variable has an associated type, namely the declared type of the variable.
''' </summary>
''' <remarks></remarks>
Public Class VariableClassification
    Inherits ExpressionClassification

    Private m_ParameterInfo As ParameterInfo
    Private m_FieldInfo As FieldInfo
    Private m_LocalBuilder As LocalBuilder

    Private m_InstanceExpression As Expression
    Private m_Parameter As Parameter
    Private m_Variable As VariableDeclaration
    Private m_Method As IMethod
    Private m_Expression As Expression

    Private m_ExpressionType As Type

    Private m_ArrayVariable As Expression
    Private m_Arguments As ArgumentList

    ReadOnly Property Method() As IMethod
        Get
            Return m_Method
        End Get
    End Property

    ReadOnly Property Expression() As Expression
        Get
            Return m_Expression
        End Get
    End Property

    ReadOnly Property ArrayVariable() As Expression
        Get
            Return m_ArrayVariable
        End Get
    End Property

    ReadOnly Property Arguments() As ArgumentList
        Get
            Return m_Arguments
        End Get
    End Property

    Public Overrides ReadOnly Property IsConstant() As Boolean
        Get
            If Me.FieldInfo IsNot Nothing Then
                If Me.FieldInfo.IsLiteral Then
                    ConstantValue = Me.FieldInfo.GetValue(Nothing)
                    Return True
                ElseIf Me.FieldInfo.IsInitOnly Then
                    Dim decAttrs() As Object

                    Dim fD As FieldDescriptor = TryCast(Me.FieldInfo, FieldDescriptor)
                    If fD IsNot Nothing Then
                        If fD.Declaration.Modifiers.ContainsAny(KS.Const) Then Return True
                        If Helper.IsEnum(fD.DeclaringType) Then Return True
                        Return False
                    End If

                    decAttrs = Me.FieldInfo.GetCustomAttributes(Compiler.TypeCache.DecimalConstantAttribute, False)
                    If decAttrs IsNot Nothing AndAlso decAttrs.Length = 1 Then
                        ConstantValue = DirectCast(decAttrs(0), System.Runtime.CompilerServices.DecimalConstantAttribute).Value()
                        Return True
                    End If

                    Dim dtAttrs() As Object
                    dtAttrs = Me.FieldInfo.GetCustomAttributes(Compiler.TypeCache.DateTimeConstantAttribute, False)
                    If dtAttrs IsNot Nothing AndAlso dtAttrs.Length = 1 Then
                        ConstantValue = DirectCast(dtAttrs(0), System.Runtime.CompilerServices.DateTimeConstantAttribute).Value()
                        Return True
                    End If


                    Return False
                Else
                    Return False
                End If
            Else
                Return False
            End If
        End Get
    End Property

    ReadOnly Property ParameterInfo() As ParameterInfo
        Get
            Return m_ParameterInfo
        End Get
    End Property

    ReadOnly Property LocalBuilder() As LocalBuilder
        Get
            If m_Variable IsNot Nothing Then
                Return m_Variable.LocalBuilder
            Else
                Return Nothing
            End If
        End Get
    End Property

    ReadOnly Property FieldInfo() As FieldInfo
        Get
            If m_Variable IsNot Nothing AndAlso m_Variable.FieldBuilder IsNot Nothing Then
                Return m_Variable.FieldBuilder
            Else
                Return m_FieldInfo
            End If
        End Get
    End Property


    ''' <summary>
    ''' Loads the value of the variable.
    ''' </summary>
    ''' <param name="Info"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Friend Function GenerateCodeAsValue(ByVal Info As EmitInfo) As Boolean
        Dim result As Boolean = True

        Helper.Assert(Info.DesiredType IsNot Nothing)

        If m_InstanceExpression IsNot Nothing Then
            result = m_InstanceExpression.GenerateCode(Info) AndAlso result
        End If

        If FieldInfo IsNot Nothing Then
            If Info.IsRHS Then
                Emitter.EmitLoadVariable(Info, FieldInfo)
            Else
                Helper.NotImplemented()
            End If
        ElseIf LocalBuilder IsNot Nothing Then
            If Info.IsRHS Then
                Emitter.EmitLoadVariable(Info, LocalBuilder)
            Else
                Helper.NotImplemented()
            End If
        ElseIf ParameterInfo IsNot Nothing Then
            Helper.Assert(m_InstanceExpression Is Nothing)
            If Info.IsRHS Then
                Emitter.EmitLoadVariable(Info, ParameterInfo)
            Else
                Helper.NotImplemented()
            End If
        ElseIf m_ArrayVariable IsNot Nothing Then
            result = Helper.EmitLoadArrayElement(Info, m_ArrayVariable, m_Arguments) AndAlso result
        ElseIf m_Expression IsNot Nothing Then
            result = m_Expression.GenerateCode(Info) AndAlso result
        Else
            Throw New InternalException(Me)
        End If

        If Info.DesiredType.IsByRef Then
            Dim elementType As Type = Info.DesiredType.GetElementType
            Dim local As LocalBuilder
            local = Info.ILGen.DeclareLocal(elementType)

            Emitter.EmitStoreVariable(Info, local)
            Emitter.EmitLoadVariableLocation(Info, local)
        End If

        Return result
    End Function

    ''' <summary>
    ''' Stores at the address of the variable.
    ''' </summary>
    ''' <param name="Info"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Friend Overrides Function GenerateCode(ByVal Info As EmitInfo) As Boolean
        Dim result As Boolean = True

        If m_Expression IsNot Nothing Then
            Return m_Expression.GenerateCode(Info)
        End If

        Helper.Assert(Info.IsRHS AndAlso Info.RHSExpression Is Nothing OrElse Info.IsLHS AndAlso Info.RHSExpression IsNot Nothing)

        If m_InstanceExpression IsNot Nothing Then
            result = m_InstanceExpression.GenerateCode(Info.Clone(True, False, m_InstanceExpression.ExpressionType)) AndAlso result
        End If

        If FieldInfo IsNot Nothing Then
            If Info.IsRHS Then
                If Info.DesiredType.IsByRef Then
                    Emitter.EmitLoadVariableLocation(Info, FieldInfo)
                Else
                    Emitter.EmitLoadVariable(Info, FieldInfo)
                End If
            Else
                Dim rInfo As EmitInfo = Info.Clone(True, False, FieldInfo.FieldType)

                Helper.Assert(Info.RHSExpression IsNot Nothing)
                Helper.Assert(Info.RHSExpression.Classification.IsValueClassification)
                result = Info.RHSExpression.Classification.GenerateCode(rInfo) AndAlso result

                Emitter.EmitConversion(FieldInfo.FieldType, Info)
                Emitter.EmitStoreField(Info, FieldInfo)
            End If
        ElseIf LocalBuilder IsNot Nothing Then
            If Info.IsRHS Then
                Emitter.EmitLoadVariable(Info, LocalBuilder)
            Else
                Dim rInfo As EmitInfo = Info.Clone(True, False, LocalBuilder.LocalType)

                Helper.Assert(Info.RHSExpression IsNot Nothing, "RHSExpression Is Nothing!")
                Helper.Assert(Info.RHSExpression.Classification.IsValueClassification OrElse Info.RHSExpression.Classification.CanBeValueClassification)
                result = Info.RHSExpression.Classification.GenerateCode(rInfo) AndAlso result

                Emitter.EmitConversion(LocalBuilder.LocalType, Info)
                Emitter.EmitStoreVariable(Info, LocalBuilder)
            End If
        ElseIf ParameterInfo IsNot Nothing Then
            Helper.Assert(m_InstanceExpression Is Nothing)
            If Info.IsRHS Then
                Helper.NotImplemented()
            Else
                Dim rInfo As EmitInfo
                Dim isByRef As Boolean = ParameterInfo.ParameterType.IsByRef
                If isByRef Then
                    Emitter.EmitLoadVariableLocation(Info, ParameterInfo)
                    rInfo = Info.Clone(True, False, ParameterInfo.ParameterType.GetElementType)
                Else
                    rInfo = Info.Clone(True, False, ParameterInfo.ParameterType)
                End If

                Helper.Assert(Info.RHSExpression IsNot Nothing, "RHSExpression Is Nothing!")
                Helper.Assert(Info.RHSExpression.Classification.IsValueClassification)
                result = Info.RHSExpression.Classification.GenerateCode(rInfo) AndAlso result

                If isByRef = False Then
                    Emitter.EmitConversion(ParameterInfo.ParameterType, Info)
                End If
                Emitter.EmitStoreVariable(Info, ParameterInfo)
            End If
        ElseIf Me.m_Variable IsNot Nothing Then
            If Info.IsRHS Then
                Helper.NotImplemented()
            Else
                Dim rInfo As EmitInfo = Info.Clone(True, False, m_Variable.VariableType)

                Helper.Assert(Info.RHSExpression IsNot Nothing)
                Helper.Assert(Info.RHSExpression.Classification.IsValueClassification)
                result = Info.RHSExpression.Classification.GenerateCode(rInfo) AndAlso result

                Emitter.EmitConversion(m_Variable.VariableType, Info)
                Emitter.EmitStoreVariable(Info, m_Variable.LocalBuilder)
                Helper.NotImplemented()
            End If
        ElseIf m_ArrayVariable IsNot Nothing Then
            If Info.IsRHS Then
                result = Me.GenerateCodeAsValue(Info) AndAlso result
            Else
                result = Helper.EmitStoreArrayElement(Info, m_ArrayVariable, m_Arguments) AndAlso result

            End If
        ElseIf m_Method IsNot Nothing Then
            If Info.IsRHS Then
                Emitter.EmitLoadVariable(Info, m_Method.DefaultReturnVariable)
            Else
                Helper.Assert(Info.RHSExpression IsNot Nothing, "RHSExpression Is Nothing!")
                Helper.Assert(Info.RHSExpression.Classification.IsValueClassification)
                result = Info.RHSExpression.Classification.GenerateCode(Info.Clone(True, False, m_Method.DefaultReturnVariable.LocalType)) AndAlso result
                Emitter.EmitStoreVariable(Info, m_Method.DefaultReturnVariable)
            End If
        Else
            Throw New InternalException(Me)
        End If

        Return result
    End Function

    ReadOnly Property InstanceExpression() As Expression
        Get
            Return m_InstanceExpression
        End Get
    End Property

    <Obsolete()> Overloads Function ReclassifyToValue() As ValueClassification
        Return New ValueClassification(Me)
    End Function

    ''' <summary>
    ''' A variable declaration which refers to the implicitly declared local variable
    ''' for methods with return values (functions and get properties)
    ''' </summary>
    ''' <param name="Parent"></param>
    ''' <param name="method"></param>
    ''' <remarks></remarks>
    Sub New(ByVal Parent As ParsedObject, ByVal method As IMethod)
        MyBase.New(Classifications.Variable, Parent)
        Helper.Assert(TypeOf method Is FunctionDeclaration OrElse TypeOf method Is IPropertyMember)
        m_Method = method
    End Sub

    Sub New(ByVal Parent As ParsedObject, ByVal parameter As Parameter)
        MyBase.New(Classifications.Variable, Parent)
        m_Parameter = parameter
        m_ParameterInfo = New ParameterDescriptor(m_Parameter)
    End Sub

    Sub New(ByVal Parent As ParsedObject, ByVal variable As VariableDeclaration, Optional ByVal InstanceExpression As Expression = Nothing)
        MyBase.New(Classifications.Variable, Parent)
        m_Variable = variable
        m_InstanceExpression = InstanceExpression
    End Sub

    Sub New(ByVal Parent As ParsedObject, ByVal Expression As Expression, ByVal ExpressionType As Type)
        MyBase.new(Classifications.Variable, Parent)
        m_Expression = Expression
        m_ExpressionType = ExpressionType
    End Sub

    Sub New(ByVal Parent As ParsedObject, ByVal variable As FieldInfo, ByVal InstanceExpression As Expression)
        MyBase.New(Classifications.Variable, Parent)
        m_FieldInfo = variable
        m_InstanceExpression = InstanceExpression
        Helper.Assert(m_InstanceExpression Is Nothing OrElse m_InstanceExpression.IsResolved)
        Helper.Assert((m_FieldInfo.IsStatic AndAlso m_InstanceExpression Is Nothing) OrElse (m_FieldInfo.IsStatic = False AndAlso m_InstanceExpression IsNot Nothing))
    End Sub

    ''' <summary>
    ''' Creates a variable classification for an array access.
    ''' </summary>
    ''' <param name="Parent"></param>
    ''' <param name="Arguments"></param>
    ''' <remarks></remarks>
    Sub New(ByVal Parent As ParsedObject, ByVal ArrayVariableExpression As Expression, ByVal Arguments As ArgumentList)
        MyBase.New(Classifications.Variable, Parent)
        m_ArrayVariable = ArrayVariableExpression
        m_Arguments = Arguments
        Helper.Assert(ArrayVariable IsNot Nothing)
        Helper.Assert(Arguments IsNot Nothing)
    End Sub

    ReadOnly Property Type() As Type 'Descriptor
        Get
            Dim result As Type
            If m_ExpressionType IsNot Nothing Then
                result = m_ExpressionType
            ElseIf m_Method IsNot Nothing Then
                result = m_Method.Signature.ReturnType
            ElseIf m_Variable IsNot Nothing Then
                result = m_Variable.VariableType
            ElseIf m_FieldInfo IsNot Nothing Then
                If m_FieldInfo.DeclaringType.IsEnum Then
                    result = m_FieldInfo.DeclaringType
                Else
                    result = m_FieldInfo.FieldType
                End If
            ElseIf m_Parameter IsNot Nothing Then
                result = m_Parameter.ParameterType
            ElseIf m_ArrayVariable IsNot Nothing Then
                result = m_ArrayVariable.ExpressionType.GetElementType
            Else
                Throw New InternalException(Me)
            End If
            Helper.Assert(result IsNot Nothing)
            Return result
        End Get
    End Property

End Class