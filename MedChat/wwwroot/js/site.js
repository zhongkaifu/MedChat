// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

Array.prototype.myJoin = function (seperator, start, end) {
    if (!start) start = 0;
    if (!end) end = this.length - 1;
    end++;
    return this.slice(start, end).join(seperator);
};

function Click2BestTurn(link) {
    var transcriptText = document.getElementById("transcriptText");
    var transcript = transcriptText.innerHTML;
    var turns = transcript.split("</div> ");
    var lastTurn = turns[turns.length - 1];
    var parts = lastTurn.split('</b>');
    var role = parts[0];
    var newLastTurn = role + "</b> " + link.innerHTML;
    turns[turns.length - 1] = newLastTurn;
    transcript = turns.join("</div> ");
    transcriptText.innerHTML = transcript;

    document.getElementById('2BestTurn').innerHTML = parts.myJoin(" ", 1);
}

function Click3BestTurn(link) {
    var transcriptText = document.getElementById("transcriptText");
    var transcript = transcriptText.innerHTML;
    var turns = transcript.split("</div> ");
    var lastTurn = turns[turns.length - 1];
    var parts = lastTurn.split('</b>');
    var role = parts[0];
    var newLastTurn = role + "</b> " + link.innerHTML;
    turns[turns.length - 1] = newLastTurn;
    transcript = turns.join("</div> ");
    transcriptText.innerHTML = transcript;
    document.getElementById('3BestTurn').innerHTML = parts.myJoin(" ", 1);
}


function UpdateInfo() {

    var AIRoleEL = document.getElementById('ddlAIRole');
    var AIRole = AIRoleEL.options[AIRoleEL.selectedIndex].text;
    var GenderEL = document.getElementById('ddlGender');
    var Gender = GenderEL.options[GenderEL.selectedIndex].text;
    var Age = document.getElementById('Age').value;
    var VisitReason = document.getElementById('VisitReason').value;
    var lang = document.getElementById('language').innerHTML;

    if (VisitReason == null || VisitReason == "") {
        VisitReason = document.getElementById('VisitReason').placeholder;
    }
  
    var prompt = "";
    if (lang == "zh") {
        var heOrShe = (Gender == "male" || Gender == "男") ? "他" : "她";
        prompt = prompt + "<div class=\"ai-message\"> 您好，我是您的虚拟远程健康咨询顾问。您是一位" + Age + "岁的" + Gender + "性新来访者。请问您的主诉是什么？</div> ";
    }
    else {
        var heOrShe = (Gender == "male") ? "He" : "She";
        prompt = prompt + "<div class=\"ai-message\"> Hi new visitor, I'm your virtual counselor for telehealth service. You are a " + Age + "-years-old " + Gender + " new visitor. What's your chief complaint?</div> ";   
    }

    if (AIRole == "visitor" || AIRole == "患者") {
        prompt = prompt + "<div class=\"user-message\"> " + VisitReason + "<img src =\"/images/patient_" + Gender + ".jpg\" width=\"50\"></div> ";
    }

    $("#transcriptText").html(prompt);
    $("#outputHPI").html("");
    $("#outputEXAM").html("");
    $("#outputPLAN").html("");
    $("#outputRESULTS").html("");

    document.getElementById('inputTurn').value = "";
    document.getElementById("chatGroup").removeAttribute("hidden");
    document.getElementById('2BestTurn').innerHTML = "";
    document.getElementById('3BestTurn').innerHTML = "";
}

var needToStop = false;

function ThumbUp(){

    var transcriptEL = document.getElementById("transcriptText");
    var transcript = transcriptEL.innerHTML;

    rq = $.ajax({
        type: "POST",
        url: "/Home/SubmitFeedback",
        data: {
            "transcript": transcript,
            "feedBackType": "1"
        },
        success: function (response) {
        }
    });
}


function ThumbDown() {

    var transcriptEL = document.getElementById("transcriptText");
    var transcript = transcriptEL.innerHTML;

    rq = $.ajax({
        type: "POST",
        url: "/Home/SubmitFeedback",
        data: {
            "transcript": transcript,
            "feedBackType": "0"
        },
        success: function (response) {
        }
    });
}


function SendTurn() {

    var contiGen = false;

    var sendAjax = function () {

        var GenderEL = document.getElementById('ddlGender');
        var Gender = GenderEL.options[GenderEL.selectedIndex].text;
        var isMale = (Gender == "male" || Gender == "男") ? true : false;

        var AIRoleEL = document.getElementById('ddlAIRole');
        var AIRole = AIRoleEL.options[AIRoleEL.selectedIndex].text;
        aiDoctor = true;
        if (AIRole != "counselor" && AIRole != "健康咨询师") {
            aiDoctor = false;
        }

        var transcriptEL = document.getElementById("transcriptText");
        var transcript = transcriptEL.innerHTML;

        var inputTurnEL = document.getElementById("inputTurn");
        var inputTurn = inputTurnEL.value;

        $.ajax({
            type: "POST",
            url: "/Home/SendTurn",
            dataType: "json",
            data: { "transcript": transcript, "inputTurn": inputTurn, "aiDoctor": aiDoctor, "contiGen": contiGen, "isMale": isMale },
            beforeSend: function () {
                $("#btnNextTurn").attr("disabled", true);
                $("#regenerateTurn").attr("disabled", true);
                $("#btnGenerateHPI").attr("disabled", true);
                $("#btnGeneratePLAN").attr("disabled", true);
                $("#btnGenerateEXAM").attr("disabled", true);
                $("#btnGenerateRESULTS").attr("disabled", true);
                document.getElementById("dotdotdot").removeAttribute("hidden");
                document.getElementById('2BestTurn').innerHTML = "";
                document.getElementById('3BestTurn').innerHTML = "";

                if (contiGen == false && (inputTurn == null || inputTurn == "")) {
                    needToStop = true;
                }
                else {
                    needToStop = false;
                }
            },
            success: function (result) {

                const parts = result.output.split('\t');
                var EOS = result.isend;

                //if (parts[0].endsWith(" EOS")) {
                //    EOS = true;
                //    parts[0] = parts[0].substring(0, parts[0].length - 4);
                //}

                $("#transcriptText").html(parts[0]);

                if (parts.length > 1) {
                    document.getElementById('2BestTurn').innerHTML = parts[1];
                }

                if (parts.length > 2) {
                    document.getElementById('3BestTurn').innerHTML = parts[2];
                }

                $("#btnNextTurn").attr("disabled", false);
                $("#regenerateTurn").attr("disabled", false);
                $("#btnGenerateHPI").attr("disabled", false);
                $("#btnGeneratePLAN").attr("disabled", false);
                $("#btnGenerateEXAM").attr("disabled", false);
                $("#btnGenerateRESULTS").attr("disabled", false);
                document.getElementById('inputTurn').value = "";
                document.getElementById("dotdotdot").setAttribute("hidden", "hidden");

                if (EOS == false && needToStop == false) {
                    contiGen = true;
                    sendAjax();
                }
            },
            error: function (err) {
                $("#btnNextTurn").attr("disabled", false);
                $("#regenerateTurn").attr("disabled", false);
                $("#btnGenerateHPI").attr("disabled", false);
                $("#btnGeneratePLAN").attr("disabled", false);
                $("#btnGenerateEXAM").attr("disabled", false);
                $("#btnGenerateRESULTS").attr("disabled", false);
                document.getElementById("dotdotdot").setAttribute("hidden", "hidden");
                document.getElementById('2BestTurn').innerHTML = "";
                document.getElementById('3BestTurn').innerHTML = "";
            }
        });
    };

    sendAjax();
}


function AssessmentPlan() {
    var AIRoleEL = document.getElementById('ddlAIRole');
    var AIRole = AIRoleEL.options[AIRoleEL.selectedIndex].text;
    aiDoctor = true;
    if (AIRole != "counselor" && AIRole != "健康咨询师") {
        aiDoctor = false;
    }

    $.ajax({
        type: "POST",
        url: "/Home/AssessmentPlan",
        dataType: "json",
        data: { "transcript": $("#transcriptText").text(), "aiDoctor": aiDoctor },
        beforeSend: function () {
            $("#btnNextTurn").attr("disabled", true);
            $("#regenerateTurn").attr("disabled", true);
            $("#btnGenerateHPI").attr("disabled", true);
            $("#btnGeneratePLAN").attr("disabled", true);
            $("#btnGenerateEXAM").attr("disabled", true);
            $("#btnGenerateRESULTS").attr("disabled", true);
            document.getElementById("dotdotdot").removeAttribute("hidden");
            document.getElementById('2BestTurn').innerHTML = "";
            document.getElementById('3BestTurn').innerHTML = "";
        },
        success: function (result) {
            $("#transcriptText").html(result.output);
            $("#btnNextTurn").attr("disabled", false);
            $("#regenerateTurn").attr("disabled", false);
            $("#btnGenerateHPI").attr("disabled", false);
            $("#btnGeneratePLAN").attr("disabled", false);
            $("#btnGenerateEXAM").attr("disabled", false);
            $("#btnGenerateRESULTS").attr("disabled", false);
            document.getElementById('inputTurn').value = "";
            document.getElementById("dotdotdot").setAttribute("hidden", "hidden");
            document.getElementById('2BestTurn').innerHTML = "";
            document.getElementById('3BestTurn').innerHTML = "";
        },
        error: function (err) {
            $("#btnNextTurn").attr("disabled", false);
            $("#regenerateTurn").attr("disabled", false);
            $("#btnGenerateHPI").attr("disabled", false);
            $("#btnGeneratePLAN").attr("disabled", false);
            $("#btnGenerateEXAM").attr("disabled", false);
            $("#btnGenerateRESULTS").attr("disabled", false);
            document.getElementById("dotdotdot").setAttribute("hidden", "hidden");
            document.getElementById('2BestTurn').innerHTML = "";
            document.getElementById('3BestTurn').innerHTML = "";
        }
    });
}


function RegenerateTurn() {

    var GenderEL = document.getElementById('ddlGender');
    var Gender = GenderEL.options[GenderEL.selectedIndex].text;
    var isMale = (Gender == "male" || Gender == "男") ? true : false;

    var AIRoleEL = document.getElementById('ddlAIRole');
    var AIRole = AIRoleEL.options[AIRoleEL.selectedIndex].text;
    aiDoctor = true;
    if (AIRole != "counselor" && AIRole != "健康咨询师") {
        aiDoctor = false;
    }
    var transcriptEL = document.getElementById("transcriptText");
    var transcript = transcriptEL.innerHTML;

    $.ajax({
        type: "POST",
        url: "/Home/RegenerateTurn",
        dataType: "json",
        data: { "transcript": transcript, "aiDoctor": aiDoctor, "isMale": isMale },
        beforeSend: function () {
            $("#btnNextTurn").attr("disabled", true);
            $("#regenerateTurn").attr("disabled", true);
            $("#btnGenerateHPI").attr("disabled", true);
            $("#btnGeneratePLAN").attr("disabled", true);
            $("#btnGenerateEXAM").attr("disabled", true);
            $("#btnGenerateRESULTS").attr("disabled", true);
            document.getElementById("dotdotdot").removeAttribute("hidden");
            document.getElementById('2BestTurn').innerHTML = "";
            document.getElementById('3BestTurn').innerHTML = "";
        },
        success: function (result) {
            const parts = result.output.split('\t');
            var EOS = false;
            if (parts[0].endsWith(" EOS")) {
                EOS = true;
                parts[0] = parts[0].substring(0, parts[0].length - 4);
            }

            $("#transcriptText").html(parts[0]);

            if (parts.length > 1) {
                document.getElementById('2BestTurn').innerHTML = parts[1];
            }

            if (parts.length > 2) {
                document.getElementById('3BestTurn').innerHTML = parts[2];
            }
            $("#btnNextTurn").attr("disabled", false);
            $("#regenerateTurn").attr("disabled", false);
            $("#btnGenerateHPI").attr("disabled", false);
            $("#btnGeneratePLAN").attr("disabled", false);
            $("#btnGenerateEXAM").attr("disabled", false);
            $("#btnGenerateRESULTS").attr("disabled", false);
            document.getElementById("dotdotdot").setAttribute("hidden", "hidden");
        },
        error: function (err) {
            $("#btnNextTurn").attr("disabled", false);
            $("#regenerateTurn").attr("disabled", false);
            $("#btnGenerateHPI").attr("disabled", false);
            $("#btnGeneratePLAN").attr("disabled", false);
            $("#btnGenerateEXAM").attr("disabled", false);
            $("#btnGenerateRESULTS").attr("disabled", false);
            document.getElementById("dotdotdot").setAttribute("hidden", "hidden");
            document.getElementById('2BestTurn').innerHTML = "";
            document.getElementById('3BestTurn').innerHTML = "";
        }
    });
}


function GenerateHPI() {
    $.ajax({
        type: "POST",
        url: "/Home/GenerateNote",
        dataType: "json",
        data: { "transcript": $("#transcriptText").text(), "tag": "hpi" },
        beforeSend: function () {
            $("#btnNextTurn").attr("disabled", true);
            $("#regenerateTurn").attr("disabled", true);
            $("#btnGenerateHPI").attr("disabled", true);
            $("#btnGeneratePLAN").attr("disabled", true);
            $("#btnGenerateEXAM").attr("disabled", true);
            $("#btnGenerateRESULTS").attr("disabled", true);
        },
        success: function (result) {
            $("#outputHPI").html(result.output);
            $("#btnNextTurn").attr("disabled", false);
            $("#regenerateTurn").attr("disabled", false);
            $("#btnGenerateHPI").attr("disabled", false);
            $("#btnGeneratePLAN").attr("disabled", false);
            $("#btnGenerateEXAM").attr("disabled", false);
            $("#btnGenerateRESULTS").attr("disabled", false);
        },
        error: function (err) {
            $("#btnNextTurn").attr("disabled", false);
            $("#regenerateTurn").attr("disabled", false);
            $("#btnGenerateHPI").attr("disabled", false);
            $("#btnGeneratePLAN").attr("disabled", false);
            $("#btnGenerateEXAM").attr("disabled", false);
            $("#btnGenerateRESULTS").attr("disabled", false);
        }
    });
}

function GeneratePLAN() {
    $.ajax({
        type: "POST",
        url: "/Home/GenerateNote",
        dataType: "json",
        data: { "transcript": $("#transcriptText").text(), "tag": "plan" },
        beforeSend: function () {
            $("#btnNextTurn").attr("disabled", true);
            $("#regenerateTurn").attr("disabled", true);
            $("#btnGenerateHPI").attr("disabled", true);
            $("#btnGeneratePLAN").attr("disabled", true);
            $("#btnGenerateEXAM").attr("disabled", true);
            $("#btnGenerateRESULTS").attr("disabled", true);
        },
        success: function (result) {
            $("#outputPLAN").html(result.output);
            $("#btnNextTurn").attr("disabled", false);
            $("#regenerateTurn").attr("disabled", false);
            $("#btnGenerateHPI").attr("disabled", false);
            $("#btnGeneratePLAN").attr("disabled", false);
            $("#btnGenerateEXAM").attr("disabled", false);
            $("#btnGenerateRESULTS").attr("disabled", false);
        },
        error: function (err) {
            $("#btnNextTurn").attr("disabled", false);
            $("#regenerateTurn").attr("disabled", false);
            $("#btnGenerateHPI").attr("disabled", false);
            $("#btnGeneratePLAN").attr("disabled", false);
            $("#btnGenerateEXAM").attr("disabled", false);
            $("#btnGenerateRESULTS").attr("disabled", false);
        }
    });
}

function GenerateEXAM() {
    $.ajax({
        type: "POST",
        url: "/Home/GenerateNote",
        dataType: "json",
        data: { "transcript": $("#transcriptText").text(), "tag": "exam" },
        beforeSend: function () {
            $("#btnNextTurn").attr("disabled", true);
            $("#regenerateTurn").attr("disabled", true);
            $("#btnGenerateHPI").attr("disabled", true);
            $("#btnGeneratePLAN").attr("disabled", true);
            $("#btnGenerateEXAM").attr("disabled", true);
            $("#btnGenerateRESULTS").attr("disabled", true);
        },
        success: function (result) {
            $("#outputEXAM").html(result.output);
            $("#btnNextTurn").attr("disabled", false);
            $("#regenerateTurn").attr("disabled", false);
            $("#btnGenerateHPI").attr("disabled", false);
            $("#btnGeneratePLAN").attr("disabled", false);
            $("#btnGenerateEXAM").attr("disabled", false);
            $("#btnGenerateRESULTS").attr("disabled", false);
        },
        error: function (err) {
            $("#btnNextTurn").attr("disabled", false);
            $("#regenerateTurn").attr("disabled", false);
            $("#btnGenerateHPI").attr("disabled", false);
            $("#btnGeneratePLAN").attr("disabled", false);
            $("#btnGenerateEXAM").attr("disabled", false);
            $("#btnGenerateRESULTS").attr("disabled", false);
        }
    });
}

function GenerateRESULTS() {
    $.ajax({
        type: "POST",
        url: "/Home/GenerateNote",
        dataType: "json",
        data: { "transcript": $("#transcriptText").text(), "tag": "results" },
        beforeSend: function () {
            $("#btnNextTurn").attr("disabled", true);
            $("#regenerateTurn").attr("disabled", true);
            $("#btnGenerateHPI").attr("disabled", true);
            $("#btnGeneratePLAN").attr("disabled", true);
            $("#btnGenerateEXAM").attr("disabled", true);
            $("#btnGenerateRESULTS").attr("disabled", true);
        },
        success: function (result) {
            $("#outputRESULTS").html(result.output);
            $("#btnNextTurn").attr("disabled", false);
            $("#regenerateTurn").attr("disabled", false);
            $("#btnGenerateHPI").attr("disabled", false);
            $("#btnGeneratePLAN").attr("disabled", false);
            $("#btnGenerateEXAM").attr("disabled", false);
            $("#btnGenerateRESULTS").attr("disabled", false);
        },
        error: function (err) {
            $("#btnNextTurn").attr("disabled", false);
            $("#regenerateTurn").attr("disabled", false);
            $("#btnGenerateHPI").attr("disabled", false);
            $("#btnGeneratePLAN").attr("disabled", false);
            $("#btnGenerateEXAM").attr("disabled", false);
            $("#btnGenerateRESULTS").attr("disabled", false);
        }
    });
}

function openNoteTab(evt, sectionName) {
    var i, tabcontent, tablinks;
    tabcontent = document.getElementsByClassName("tabcontent");
    for (i = 0; i < tabcontent.length; i++) {
        tabcontent[i].style.display = "none";
    }
    tablinks = document.getElementsByClassName("tablinks");
    for (i = 0; i < tablinks.length; i++) {
        tablinks[i].className = tablinks[i].className.replace(" active", "");
    }
    document.getElementById(sectionName).style.display = "block";
    evt.currentTarget.className += " active";
}

function selAIRole(value) {
    if (value == "counselor") {
        document.getElementById("pVisitReason").setAttribute("hidden", "hidden");
    }
    else {
        document.getElementById("pVisitReason").removeAttribute("hidden");
    }

    $("#transcriptText").html(prompt);
    $("#outputHPI").html("");
    $("#outputEXAM").html("");
    $("#outputPLAN").html("");
    $("#outputRESULTS").html("");

    document.getElementById('inputTurn').value = "";
}