Module ExplicitConversionByteToLong1
    Function Main() As Integer
        Dim result As Boolean
        Dim value1 As Byte
        Dim value2 As Long
        Dim const2 As Long

        value1 = CByte(10)
        value2 = CLng(value1)
        const2 = CLng(CByte(10))

        result = value2 = const2

        If result = False Then
            System.Console.WriteLine("FAIL ExplicitConversionByteToLong1")
            Return 1
        End If
    End Function
End Module
