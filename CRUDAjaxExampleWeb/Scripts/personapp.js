$(document).ready(function () {
    var personId = $('#person-id-container').text();

    $('input[type="text"][name="BirthDate"]').datepicker();

    $('#age-calc-btn').click(function () {
        $('.loading-indicator-container').show();
        $.post('/home/CalculateAge',
        { id: personId },
        function (data) {
            $('.age-result-container').text(data.Age);
            $('.loading-indicator-container').hide();
        })
    });
});