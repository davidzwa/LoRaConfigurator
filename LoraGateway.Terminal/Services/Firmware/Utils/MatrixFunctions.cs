using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services.Firmware.Utils;

/// <summary>
///     https://github.com/elsheimy/Elsheimy.Components.Linears/tree/main/MatrixFunctions
/// </summary>
public static class MatrixFunctions
{
    private static readonly GField unity = new(1);
    private static readonly GField nil = new(0);

    /// <summary>
    ///     Reduces matrix to row-echelon (REF/Gauss) or reduced row-echelon (RREF/Gauss-Jordan) form.
    /// </summary>
    public static GField[,] Reduce(GField[,] input)
    {
        return Eliminate(input);
    }

    /// <summary>
    ///     Reduces matrix to row-echelon (REF/Gauss) or reduced row-echelon (RREF/Gauss-Jordan) form and solves for augmented
    ///     columns (if any.)
    /// </summary>
    public static GField[,] Eliminate(GField[,] input, int augmentedCols = 0)
    {
        var totalRowCount = input.GetLength(0);
        var totalColCount = input.GetLength(1);

        if (augmentedCols >= totalColCount)
            throw new ArgumentException("Too many augmented columns for total column count", nameof(augmentedCols));

        // We dont collect a result, just return the full matrix result
        // MatrixEliminationResult result = new MatrixEliminationResult();

        var output = input.Clone() as GField[,];

        if (output == null) throw new Exception("Cloned matrix was null");

        // number of pivots found
        var numPivots = 0;

        // loop through columns, exclude augmented columns
        for (var col = 0; col < totalColCount - augmentedCols; col++)
        {
            var pivotRow = FindPivot(output, numPivots, col, totalRowCount);

            if (pivotRow == null)
                continue; // no pivots, go to another column

            ReduceRow(output, pivotRow.Value, col, totalColCount);

            SwitchRows(output, pivotRow.Value, numPivots, totalColCount);

            pivotRow = numPivots;
            numPivots++;

            // Require Reduced form unconditionally
            // if (form == MatrixReductionForm.ReducedRowEchelonForm)
            for (var tmpRow = 0; tmpRow < pivotRow; tmpRow++)
                EliminateRow(output, tmpRow, pivotRow.Value, col, totalColCount);

            // Eliminate Next Rows
            for (var tmpRow = pivotRow.Value; tmpRow < totalRowCount; tmpRow++)
                EliminateRow(output, tmpRow, pivotRow.Value, col, totalColCount);
        }

        // result.FullMatrix = output;
        // result.UnknownsCount = totalColCount - result.AugmentedColumnCount;
        // result.TotalRowCount = totalRowCount;
        // result.TotalColumnCount = totalColCount;
        // result.AugmentedColumnCount = augmentedCols;

        // We ignore augmented cols for now
        // result.AugmentedColumns = ExtractColumns(output, result.UnknownsCount, totalColCount - 1);
        // if (augmentedCols > 0 && form == MatrixReductionForm.ReducedRowEchelonForm)
        //     // matrix has solution 
        //     result = FindSolution(result);

        return output;
    }


    private static int? FindPivot(GField[,] input, int startRow, int col, int rowCount)
    {
        for (var i = startRow; i < rowCount; i++)
            if (input[i, col] != nil)
                return i;

        return null;
    }

    private static void SwitchRows(GField[,] input, int row1, int row2, int colCount)
    {
        if (row1 == row2)
            return;

        for (var col = 0; col < colCount; col++)
            (input[row1, col], input[row2, col]) = (input[row2, col], input[row1, col]);
    }

    private static void ReduceRow(GField[,] input, int row, int col, int colCount)
    {
        var coefficient = unity / input[row, col];

        if (coefficient == unity)
            return;

        for (; col < colCount; col++)
            input[row, col] *= coefficient;
    }

    /// <summary>
    ///     Eliminates row using another pivot row.
    /// </summary>
    private static void EliminateRow(GField[,] input, int row, int pivotRow, int pivotCol, int colCount)
    {
        if (pivotRow == row)
            return;

        if (input[row, pivotCol] == nil)
            return;

        var coefficient = input[row, pivotCol];
        for (var col = pivotCol; col < colCount; col++) input[row, col] -= input[pivotRow, col] * coefficient;
    }
}