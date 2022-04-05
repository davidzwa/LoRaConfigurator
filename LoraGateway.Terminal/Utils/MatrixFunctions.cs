using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services.Firmware.Utils;

/// <summary>
///     https://github.com/elsheimy/Elsheimy.Components.Linears/tree/main/MatrixFunctions
/// </summary>
public static class MatrixFunctions
{
    private static readonly GFSymbol Unity = new(1);
    private static readonly GFSymbol Nil = new(0);

    /// <summary>
    ///     Reduces matrix to row-echelon (REF/Gauss) or reduced row-echelon (RREF/Gauss-Jordan) form and solves for augmented
    ///     columns (if any.)
    /// </summary>
    public static GFSymbol[,] Eliminate(GFSymbol[,] input, int augmentedCols)
    {
        var totalRowCount = input.GetLength(0);
        var totalColCount = input.GetLength(1);

        if (augmentedCols >= totalColCount)
            throw new ArgumentException("Too many augmented columns for total column count", nameof(augmentedCols));

        var output = input.Clone() as GFSymbol[,];
        if (output == null) throw new Exception("Cloned matrix was null");
        
        // Loop through columns, exclude augmented columns
        var numPivots = 0;
        for (var col = 0; col < totalColCount - augmentedCols; col++)
        {
            var pivotRow = FindPivot(output, numPivots, col, totalRowCount);

            if (pivotRow == null)
                continue; // no pivots, go to another column

            ReduceRow(output, pivotRow.Value, col, totalColCount);

            SwitchRows(output, pivotRow.Value, numPivots, totalColCount);

            pivotRow = numPivots;
            numPivots++;

            for (var tmpRow = 0; tmpRow < pivotRow; tmpRow++) {
                EliminateRow(output, tmpRow, pivotRow.Value, col, totalColCount);
            }

            // Eliminate Next Rows
            for (var tmpRow = pivotRow.Value; tmpRow < totalRowCount; tmpRow++) {
                EliminateRow(output, tmpRow, pivotRow.Value, col, totalColCount);
            }
        }

        return output;
    }


    private static int? FindPivot(GFSymbol[,] input, int startRow, int col, int rowCount)
    {
        for (var i = startRow; i < rowCount; i++)
            if (input[i, col] != Nil)
                return i;

        return null;
    }

    private static void SwitchRows(GFSymbol[,] input, int row1, int row2, int colCount)
    {
        if (row1 == row2)
            return;

        for (var col = 0; col < colCount; col++)
            (input[row1, col], input[row2, col]) = (input[row2, col], input[row1, col]);
    }

    private static void ReduceRow(GFSymbol[,] input, int row, int col, int colCount)
    {
        var coefficient = Unity / input[row, col];

        if (coefficient == Unity)
            return;

        for (; col < colCount; col++)
            input[row, col] *= coefficient;
    }

    /// <summary>
    ///     Eliminates row using another pivot row.
    /// </summary>
    private static void EliminateRow(GFSymbol[,] input, int row, int pivotRow, int pivotCol, int colCount)
    {
        if (pivotRow == row)
            return;

        if (input[row, pivotCol] == Nil)
            return;

        var coefficient = input[row, pivotCol];
        for (var col = pivotCol; col < colCount; col++) input[row, col] -= input[pivotRow, col] * coefficient;
    }
}